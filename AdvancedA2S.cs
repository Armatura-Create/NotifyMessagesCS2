using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NotifyMessages;

/// Результат парсинга A2S_INFO
public class A2SInfoResponse
{
    public string ServerName { get; set; } = "";
    public string Map { get; set; } = "";
    public byte Players { get; set; }
    public byte MaxPlayers { get; set; }
    public byte Bots { get; set; }
    public string GameDir { get; set; } = "";
    public string GameDesc { get; set; } = "";
    public byte Protocol { get; set; }
    public short AppID { get; set; }
}

/// Асинхронный A2S_INFO с поддержкой challenge и split‑packets + таймаут
public static class AdvancedA2S
{
    // https://developer.valvesoftware.com/wiki/Server_queries#A2S_INFO
    private static readonly byte[] A2S_INFO_HEADER = { 0xFF, 0xFF, 0xFF, 0xFF, 0x54 }; // 'T'
    private static readonly byte[] A2S_INFO_STRING = Encoding.ASCII.GetBytes("Source Engine Query\0");

    /// Отправляет запрос A2S_INFO, возвращает распарсенный ответ или null (если оффлайн/таймаут)
    public static async Task<A2SInfoResponse?> GetServerInfoAsync(string ipOrHost, ushort port, int timeoutMs = 1000,
        CancellationToken ct = default)
    {
        try
        {
            var endpoint = await ResolveEndPointAsync(ipOrHost, port, ct).ConfigureAwait(false);
            if (endpoint == null)
                return null;

            var raw = await GetA2SInfoRawAsync(endpoint, timeoutMs, ct).ConfigureAwait(false);
            if (raw == null || raw.Length < 5)
                return null;

            int index = 0;
            index += 4; // 0xFF FF FF FF
            byte header = raw[index++];
            if (header != 0x49) // 0x49 = 'I' (A2S_INFO)
                return null;

            var response = new A2SInfoResponse();

            response.Protocol = raw[index++];

            response.ServerName = ReadNullTerminatedString(raw, ref index);
            response.Map = ReadNullTerminatedString(raw, ref index);
            response.GameDir = ReadNullTerminatedString(raw, ref index);
            response.GameDesc = ReadNullTerminatedString(raw, ref index);

            response.AppID = BitConverter.ToInt16(raw, index);
            index += 2;

            response.Players = raw[index++];
            response.MaxPlayers = raw[index++];
            response.Bots = raw[index++];

            // skip ServerType(1), Environment(1), Visibility(1), VAC(1) — если есть
            // в некоторых играх там могут быть дополнительные поля; если нужно — допарсить.

            return response;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            LogService.Error("[AdvancedA2S] Error", ex);
            return null;
        }
    }

    private static async Task<IPEndPoint?> ResolveEndPointAsync(string ipOrHost, ushort port, CancellationToken ct)
    {
        if (IPAddress.TryParse(ipOrHost, out var ip))
            return new IPEndPoint(ip, port);

        try
        {
            var entry = await Dns.GetHostEntryAsync(ipOrHost).WaitAsync(TimeSpan.FromSeconds(2), ct)
                .ConfigureAwait(false);
            foreach (var addr in entry.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork ||
                    addr.AddressFamily == AddressFamily.InterNetworkV6)
                    return new IPEndPoint(addr, port);
            }
        }
        catch
        {
            /* ignore */
        }

        return null;
    }

    /// Получение сырых данных A2S_INFO (challenge + split packets) c таймаутом
    private static async Task<byte[]?> GetA2SInfoRawAsync(IPEndPoint endpoint, int timeoutMs, CancellationToken ct)
    {
        using var client = new UdpClient(endpoint.AddressFamily);
        client.Client.ReceiveTimeout = timeoutMs;
        client.Client.SendTimeout = timeoutMs;

        var request = new byte[A2S_INFO_HEADER.Length + A2S_INFO_STRING.Length];
        Buffer.BlockCopy(A2S_INFO_HEADER, 0, request, 0, A2S_INFO_HEADER.Length);
        Buffer.BlockCopy(A2S_INFO_STRING, 0, request, A2S_INFO_HEADER.Length, A2S_INFO_STRING.Length);

        // первичный запрос
        await client.SendAsync(request, request.Length, endpoint);

        var data = await ReceiveAsync(client, endpoint, timeoutMs, ct);
        if (data == null) return null;

        // challenge?
        if (IsChallengePacket(data))
        {
            var challenge = new byte[data.Length - 5];
            Buffer.BlockCopy(data, 5, challenge, 0, challenge.Length);

            var newRequest = new byte[request.Length + challenge.Length];
            Buffer.BlockCopy(request, 0, newRequest, 0, request.Length);
            Buffer.BlockCopy(challenge, 0, newRequest, request.Length, challenge.Length);

            await client.SendAsync(newRequest, newRequest.Length, endpoint);
            data = await ReceiveAsync(client, endpoint, timeoutMs, ct);
            if (data == null) return null;
        }

        // split?
        if (IsSplitPacket(data))
            data = await CollectSplitPacketsAsync(data, client, endpoint, timeoutMs, ct);

        return data;
    }

    private static async Task<byte[]?> ReceiveAsync(UdpClient client, IPEndPoint endpoint, int timeoutMs,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            #if NET7_0_OR_GREATER
                var result = await client.ReceiveAsync(cts.Token);
            #else
                var task = client.ReceiveAsync();
                var finished = await Task.WhenAny(task, Task.Delay(timeoutMs, cts.Token));
                if (finished != task) return null;
                var result = task.Result;
            #endif
            // endpoint может измениться (ответ от сервера) — используем result.RemoteEndPoint, но нам не обязательно совпадение
            return result.Buffer;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static bool IsChallengePacket(byte[] data)
    {
        return data.Length >= 5 &&
               data[0] == 0xFF && data[1] == 0xFF && data[2] == 0xFF && data[3] == 0xFF &&
               data[4] == 0x41; // 'A'
    }

    private static bool IsSplitPacket(byte[] data)
    {
        return data.Length >= 5 &&
               data[0] == 0xFF && data[1] == 0xFF && data[2] == 0xFF && data[3] == 0xFF &&
               data[4] == 0xFE;
    }

    private static async Task<byte[]> CollectSplitPacketsAsync(byte[] firstPacket, UdpClient client,
        IPEndPoint endpoint, int timeoutMs, CancellationToken ct)
    {
        // формат: 0..3 = 0xFF FF FF FF, 4=0xFE, 5..6=packetID, 7=packetsCount, 8=index, 9..payload
        var fragments = new Dictionary<byte, byte[]>();
        byte packetsCount = firstPacket[7];
        byte packetIndex = firstPacket[8];

        var payload = new byte[firstPacket.Length - 9];
        Buffer.BlockCopy(firstPacket, 9, payload, 0, payload.Length);
        fragments[packetIndex] = payload;

        if (packetsCount == 1)
            return payload;

        var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(200, timeoutMs));
        while (fragments.Count < packetsCount && DateTime.UtcNow < deadline)
        {
            var newData = await ReceiveAsync(client, endpoint, Math.Max(50, timeoutMs / 2), ct);
            if (newData == null) break;
            if (!IsSplitPacket(newData)) continue;

            var idx = newData[8];
            var newPayload = new byte[newData.Length - 9];
            Buffer.BlockCopy(newData, 9, newPayload, 0, newPayload.Length);

            if (!fragments.ContainsKey(idx))
                fragments[idx] = newPayload;
        }

        var combined = new List<byte>(fragments.Count * 1200);
        for (byte i = 0; i < packetsCount; i++)
        {
            if (fragments.TryGetValue(i, out var frag))
                combined.AddRange(frag);
        }

        return combined.ToArray();
    }

    private static string ReadNullTerminatedString(byte[] data, ref int index)
    {
        var sb = new StringBuilder();
        while (index < data.Length)
        {
            if (data[index] == 0)
            {
                index++;
                break;
            }

            sb.Append((char)data[index]);
            index++;
        }

        return sb.ToString();
    }
}