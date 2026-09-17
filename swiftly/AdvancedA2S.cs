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

/// Асинхронный A2S_INFO с поддержкой challenge и split-packets + таймаут.
///
/// Всё, что приходит по сети, — недоверенный ввод: каждое чтение проверяет границы буфера,
/// строки декодируются как UTF-8, а ответы принимаются только с адреса опрашиваемого сервера.
public static class AdvancedA2S
{
    // https://developer.valvesoftware.com/wiki/Server_queries#A2S_INFO
    private static readonly byte[] A2S_INFO_HEADER = { 0xFF, 0xFF, 0xFF, 0xFF, 0x54 }; // 'T'
    private static readonly byte[] A2S_INFO_STRING = Encoding.ASCII.GetBytes("Source Engine Query\0");

    // Source-формат split-заголовка: 4 (0xFFFFFFFE) + 4 (ID) + 1 (total) + 1 (number) + 2 (splitSize)
    private const int SplitHeaderSize = 12;

    // Здравые пределы, чтобы кривой/злонамеренный ответ не заставил нас копить память
    private const int MaxSplitPackets = 32;
    private const int MaxReassembledBytes = 256 * 1024;

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
            if (raw == null)
                return null;

            return ParseInfo(raw);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            // Не логируем здесь - исключение обрабатывается выше в ServerStatusService
            return null;
        }
    }

    /// Разбор тела A2S_INFO. Возвращает null на любом усечённом/некорректном пакете.
    internal static A2SInfoResponse? ParseInfo(byte[] raw)
    {
        var index = 0;

        // 0xFF FF FF FF + тип ответа
        if (!CanRead(raw, index, 5)) return null;
        index += 4;

        if (raw[index++] != 0x49) // 0x49 = 'I' (A2S_INFO)
            return null;

        var response = new A2SInfoResponse();

        if (!CanRead(raw, index, 1)) return null;
        response.Protocol = raw[index++];

        if (!TryReadNullTerminatedString(raw, ref index, out var serverName)) return null;
        if (!TryReadNullTerminatedString(raw, ref index, out var map)) return null;
        if (!TryReadNullTerminatedString(raw, ref index, out var gameDir)) return null;
        if (!TryReadNullTerminatedString(raw, ref index, out var gameDesc)) return null;

        response.ServerName = serverName;
        response.Map = map;
        response.GameDir = gameDir;
        response.GameDesc = gameDesc;

        if (!CanRead(raw, index, 2)) return null;
        response.AppID = BitConverter.ToInt16(raw, index);
        index += 2;

        if (!CanRead(raw, index, 3)) return null;
        response.Players = raw[index++];
        response.MaxPlayers = raw[index++];
        response.Bots = raw[index];

        // Далее идут ServerType/Environment/Visibility/VAC и опциональный EDF — нам не нужны.
        return response;
    }

    private static bool CanRead(byte[] data, int index, int count)
        => index >= 0 && count >= 0 && index + count <= data.Length;

    private static async Task<IPEndPoint?> ResolveEndPointAsync(string ipOrHost, ushort port, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ipOrHost))
            return null;

        if (IPAddress.TryParse(ipOrHost, out var ip))
            return new IPEndPoint(ip, port);

        try
        {
            var entry = await Dns.GetHostEntryAsync(ipOrHost, ct).WaitAsync(TimeSpan.FromSeconds(2), ct)
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

        var request = new byte[A2S_INFO_HEADER.Length + A2S_INFO_STRING.Length];
        Buffer.BlockCopy(A2S_INFO_HEADER, 0, request, 0, A2S_INFO_HEADER.Length);
        Buffer.BlockCopy(A2S_INFO_STRING, 0, request, A2S_INFO_HEADER.Length, A2S_INFO_STRING.Length);

        // первичный запрос
        await client.SendAsync(request, request.Length, endpoint).ConfigureAwait(false);

        var data = await ReceiveAsync(client, endpoint, timeoutMs, ct).ConfigureAwait(false);
        if (data == null) return null;

        // challenge? (0xFFFFFFFF 'A' + 4 байта challenge)
        if (IsChallengePacket(data))
        {
            var challengeLength = data.Length - 5;
            if (challengeLength <= 0) return null;

            var newRequest = new byte[request.Length + challengeLength];
            Buffer.BlockCopy(request, 0, newRequest, 0, request.Length);
            Buffer.BlockCopy(data, 5, newRequest, request.Length, challengeLength);

            await client.SendAsync(newRequest, newRequest.Length, endpoint).ConfigureAwait(false);
            data = await ReceiveAsync(client, endpoint, timeoutMs, ct).ConfigureAwait(false);
            if (data == null) return null;
        }

        // split?
        if (IsSplitPacket(data))
            data = await CollectSplitPacketsAsync(data, client, endpoint, timeoutMs, ct).ConfigureAwait(false);

        return data;
    }

    private static async Task<byte[]?> ReceiveAsync(UdpClient client, IPEndPoint endpoint, int timeoutMs,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            while (true)
            {
                var result = await client.ReceiveAsync(cts.Token).ConfigureAwait(false);

                // Принимаем только ответы с адреса, который сами и опрашивали:
                // иначе любой посторонний хост мог подменить статус чужого сервера.
                if (!result.RemoteEndPoint.Address.Equals(endpoint.Address))
                    continue;

                return result.Buffer;
            }
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
        return data.Length >= SplitHeaderSize &&
               data[0] == 0xFF && data[1] == 0xFF && data[2] == 0xFF && data[3] == 0xFF &&
               data[4] == 0xFE;
    }

    private static async Task<byte[]?> CollectSplitPacketsAsync(byte[] firstPacket, UdpClient client,
        IPEndPoint endpoint, int timeoutMs, CancellationToken ct)
    {
        if (!TrySplitPayload(firstPacket, out var packetId, out var packetsCount, out var packetIndex,
                out var payload))
            return null;

        if (packetsCount == 0 || packetsCount > MaxSplitPackets)
            return null;

        var fragments = new Dictionary<byte, byte[]> { [packetIndex] = payload };
        var totalBytes = payload.Length;

        if (packetsCount == 1)
            return payload;

        var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(200, timeoutMs));
        while (fragments.Count < packetsCount && DateTime.UtcNow < deadline)
        {
            var newData = await ReceiveAsync(client, endpoint, Math.Max(50, timeoutMs / 2), ct).ConfigureAwait(false);
            if (newData == null) break;
            if (!IsSplitPacket(newData)) continue;

            if (!TrySplitPayload(newData, out var id, out _, out var idx, out var newPayload))
                continue;

            // Фрагменты чужого ответа не подмешиваем
            if (id != packetId) continue;
            if (idx >= packetsCount) continue;
            if (fragments.ContainsKey(idx)) continue;

            totalBytes += newPayload.Length;
            if (totalBytes > MaxReassembledBytes) return null;

            fragments[idx] = newPayload;
        }

        // Неполная сборка даст мусор при разборе — честнее считать сервер недоступным
        if (fragments.Count != packetsCount)
            return null;

        var combined = new byte[totalBytes];
        var offset = 0;
        for (byte i = 0; i < packetsCount; i++)
        {
            var frag = fragments[i];
            Buffer.BlockCopy(frag, 0, combined, offset, frag.Length);
            offset += frag.Length;
        }

        return combined;
    }

    private static bool TrySplitPayload(byte[] data, out int packetId, out byte packetsCount, out byte packetIndex,
        out byte[] payload)
    {
        packetId = 0;
        packetsCount = 0;
        packetIndex = 0;
        payload = Array.Empty<byte>();

        if (data.Length < SplitHeaderSize) return false;

        packetId = BitConverter.ToInt32(data, 4);

        // Старший бит ID = сжатый ответ (bzip2). CS2 такое не шлёт, разбирать не умеем.
        if ((packetId & unchecked((int)0x80000000)) != 0) return false;

        packetsCount = data[8];
        packetIndex = data[9];

        payload = new byte[data.Length - SplitHeaderSize];
        Buffer.BlockCopy(data, SplitHeaderSize, payload, 0, payload.Length);
        return true;
    }

    /// Читает null-terminated UTF-8 строку. Раньше байты кастовались в char напрямую,
    /// из-за чего кириллические имена серверов и карт превращались в мусор.
    internal static bool TryReadNullTerminatedString(byte[] data, ref int index, out string value)
    {
        value = string.Empty;
        if (index < 0 || index >= data.Length) return false;

        var start = index;
        while (index < data.Length && data[index] != 0)
            index++;

        if (index >= data.Length) return false; // нет завершающего нуля — пакет усечён

        value = Encoding.UTF8.GetString(data, start, index - start);
        index++; // пропускаем '\0'
        return true;
    }
}
