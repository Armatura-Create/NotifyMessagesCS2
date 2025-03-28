using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

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
    // и т.д. — при желании можно расширять 
}

/// Класс, который пытается корректно сделать A2S_INFO запрос (с challenge и split-packets)
public static class AdvancedA2S
{
    // См. https://developer.valvesoftware.com/wiki/Server_queries#A2S_INFO
    private static readonly byte[] A2S_INFO_HEADER = { 0xFF, 0xFF, 0xFF, 0xFF, 0x54 }; // 0x54 = 'T'
    private static readonly byte[] A2S_INFO_STRING = Encoding.ASCII.GetBytes("Source Engine Query\0");

    /// Отправляет запрос A2S_INFO, возвращает распарсенный A2SInfoResponse либо null, если не удалось
    public static A2SInfoResponse? GetServerInfo(string ip, ushort port, int timeoutMs = 2000)
    {
        try
        {
            // 1) Получаем сырые данные (учитывая challenge, мультипакеты и т.д.)
            var raw = GetA2SInfoRaw(ip, port, timeoutMs);
            if (raw == null || raw.Length < 5)
            {
                Console.WriteLine("[AdvancedA2S] Нет данных или слишком короткий ответ");
                return null;
            }

            // 2) Проверяем, что это 0xFF 0xFF 0xFF 0xFF 0x49
            // (для одного пакета), либо сразу после склейки
            int index = 0;
            // 4 байта 0xFF FF FF FF
            index += 4;
            // Следующий байт должен быть 0x49 (A2S_INFO).
            byte header = raw[index++];
            if (header != 0x49)
            {
                Console.WriteLine($"[AdvancedA2S] Неожиданный тип пакета: 0x{header:X2}");
                return null;
            }

            // Теперь парсим поля (см. https://developer.valvesoftware.com/wiki/Server_queries#A2S_INFO)
            var response = new A2SInfoResponse();

            // Протокол
            response.Protocol = raw[index++];

            // Имя сервера (string, null-terminated)
            response.ServerName = ReadNullTerminatedString(raw, ref index);

            // Текущая карта
            response.Map = ReadNullTerminatedString(raw, ref index);

            // Папка (GameDir)
            response.GameDir = ReadNullTerminatedString(raw, ref index);

            // Описание игры
            response.GameDesc = ReadNullTerminatedString(raw, ref index);

            // AppID (2 байта)
            response.AppID = BitConverter.ToInt16(raw, index);
            index += 2;

            // Кол-во игроков
            response.Players = raw[index++];
            // Макс. кол-во игроков
            response.MaxPlayers = raw[index++];
            // Кол-во ботов
            response.Bots = raw[index++];
            index++;

            // Далее ещё куча полей — при необходимости допарсить
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdvancedA2S] Ошибка: {ex.Message}");
            return null;
        }
    }

    /// Возвращает сырые данные A2S_INFO (после возможного challenge и сборки split-пакетов)
    private static byte[]? GetA2SInfoRaw(string ip, ushort port, int timeoutMs)
    {
        // Собираем байты для первичного запроса: 0xFF FF FF FF 0x54 + "Source Engine Query\0"
        var request = new byte[A2S_INFO_HEADER.Length + A2S_INFO_STRING.Length];
        Buffer.BlockCopy(A2S_INFO_HEADER, 0, request, 0, A2S_INFO_HEADER.Length);
        Buffer.BlockCopy(A2S_INFO_STRING, 0, request, A2S_INFO_HEADER.Length, A2S_INFO_STRING.Length);

        using var client = new UdpClient();
        client.Client.ReceiveTimeout = timeoutMs;
        client.Client.SendTimeout = timeoutMs;

        var endpoint = new IPEndPoint(IPAddress.Parse(ip), port);

        // Отправляем
        client.Send(request, request.Length, endpoint);

        var data = ReceiveA2SResponse(client, ref endpoint);
        if (data == null) return null;

        // Если это CHALLENGE (0x41) — повторяем запрос, добавляя challenge
        // Формат: 0xFF FF FF FF 0x41 + 4 байта challenge
        if (IsChallengePacket(data))
        {
            var challenge = new byte[data.Length - 5];
            // Скопируем challenge (байты после 0xFF FF FF FF 0x41)
            Buffer.BlockCopy(data, 5, challenge, 0, challenge.Length);

            // Формируем повторный запрос A2S_INFO c challenge в конце
            var newRequest = new byte[request.Length + challenge.Length];
            Buffer.BlockCopy(request, 0, newRequest, 0, request.Length);
            Buffer.BlockCopy(challenge, 0, newRequest, request.Length, challenge.Length);

            client.Send(newRequest, newRequest.Length, endpoint);
            data = ReceiveA2SResponse(client, ref endpoint);
            if (data == null) return null;
        }

        // Если это split-пакет (0xFE), собираем все части
        if (IsSplitPacket(data))
        {
            // Вызовем метод, который соберёт все куски в один массив
            data = CollectSplitPackets(data, client, endpoint);
        }

        return data;
    }

    /// Получаем один пакет из UDP
    private static byte[]? ReceiveA2SResponse(UdpClient client, ref IPEndPoint endpoint)
    {
        try
        {
            return client.Receive(ref endpoint);
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"[AdvancedA2S] SocketException: {ex.Message}");
            return null;
        }
    }

    /// Проверяем, что пакет — Challenge-пакет (0x41)
    private static bool IsChallengePacket(byte[] data)
    {
        if (data.Length < 5) return false;
        // 0..3 = 0xFF FF FF FF, 4 = 0x41
        return data[0] == 0xFF && data[1] == 0xFF && data[2] == 0xFF && data[3] == 0xFF && data[4] == 0x41;
    }

    /// Проверяем, что пакет — Split-пакет (0xFE)
    private static bool IsSplitPacket(byte[] data)
    {
        if (data.Length < 5) return false;
        // 0..3 = 0xFF FF FF FF, 4 = 0xFE
        return data[0] == 0xFF && data[1] == 0xFF && data[2] == 0xFF && data[3] == 0xFF && data[4] == 0xFE;
    }

    /// Сборка split-пакетов в один
    private static byte[] CollectSplitPackets(byte[] firstPacket, UdpClient client, IPEndPoint endpoint)
    {
        // Формат split-пакета (см. https://developer.valvesoftware.com/wiki/Server_queries#Multiple-packet_Responses)
        //  0..3 = 0xFF FF FF FF
        //  4 = 0xFE
        //  5..6 = short packetID (?)
        //  7 = кол-во пакетов
        //  8 = номер пакета (начиная с 0)
        //  9.. ? (payload)

        // Сохраним все фрагменты в словарь, ключ — номер пакета
        var fragments = new Dictionary<byte, byte[]>();
        byte packetsCount = firstPacket[7];
        byte packetIndex = firstPacket[8];

        // Вырезаем данные после заголовка
        var payload = new byte[firstPacket.Length - 9];
        Buffer.BlockCopy(firstPacket, 9, payload, 0, payload.Length);
        fragments[packetIndex] = payload;

        // Если только один пакет ( packetsCount == 1 ), то сразу возвращаем
        if (packetsCount == 1)
            return payload;

        // Иначе, нужно принять остальные (packetsCount - 1)
        while (fragments.Count < packetsCount)
        {
            var newData = ReceiveA2SResponse(client, ref endpoint);
            if (newData == null) break;
            if (!IsSplitPacket(newData)) break; // Может быть что-то не то

            var idx = newData[8];
            var newPayload = new byte[newData.Length - 9];
            Buffer.BlockCopy(newData, 9, newPayload, 0, newPayload.Length);

            if (!fragments.ContainsKey(idx))
                fragments[idx] = newPayload;
        }

        // Склеиваем все пакеты по порядку индексов (0..packetsCount-1)
        var combined = new List<byte>();
        for (byte i = 0; i < packetsCount; i++)
        {
            if (fragments.TryGetValue(i, out var frag))
                combined.AddRange(frag);
        }

        return combined.ToArray();
    }

    /// Чтение null-terminated строки из массива
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
