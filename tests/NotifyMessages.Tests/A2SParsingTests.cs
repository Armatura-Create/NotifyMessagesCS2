using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace NotifyMessages.Tests;

/// A2S-ответ приходит по сети от произвольного хоста — это недоверенный ввод.
/// Разбор обязан выдерживать усечённые и мусорные пакеты, не бросая исключений.
public class A2SParsingTests
{
    private static byte[] BuildInfoPacket(string serverName, string map, byte players, byte maxPlayers, byte bots)
    {
        var bytes = new List<byte> { 0xFF, 0xFF, 0xFF, 0xFF, 0x49, 17 }; // header + 'I' + protocol
        void Str(string v)
        {
            bytes.AddRange(Encoding.UTF8.GetBytes(v));
            bytes.Add(0);
        }

        Str(serverName);
        Str(map);
        Str("csgo");      // GameDir
        Str("CS2");       // GameDesc
        bytes.AddRange(BitConverter.GetBytes((short)730)); // AppID
        bytes.Add(players);
        bytes.Add(maxPlayers);
        bytes.Add(bots);
        return bytes.ToArray();
    }

    [Fact]
    public void ParseInfo_ReadsWellFormedPacket()
    {
        var info = AdvancedA2S.ParseInfo(BuildInfoPacket("My Server", "de_dust2", 12, 32, 2));

        Assert.NotNull(info);
        Assert.Equal("My Server", info!.ServerName);
        Assert.Equal("de_dust2", info.Map);
        Assert.Equal(12, info.Players);
        Assert.Equal(32, info.MaxPlayers);
        Assert.Equal(2, info.Bots);
    }

    [Fact]
    public void ParseInfo_DecodesUtf8Names()
    {
        // Регрессия: раньше байты кастовались в char и кириллица превращалась в мусор
        var info = AdvancedA2S.ParseInfo(BuildInfoPacket("Сервер Армату́ра", "de_инферно", 1, 10, 0));

        Assert.NotNull(info);
        Assert.Equal("Сервер Армату́ра", info!.ServerName);
        Assert.Equal("de_инферно", info.Map);
    }

    [Fact]
    public void ParseInfo_RejectsTruncatedPacketWithoutThrowing()
    {
        var full = BuildInfoPacket("Server", "de_nuke", 5, 10, 0);

        // Любой префикс полного пакета не должен ронять разбор
        for (var len = 0; len < full.Length; len++)
        {
            var truncated = full[..len];
            var ex = Record.Exception(() => AdvancedA2S.ParseInfo(truncated));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void ParseInfo_RejectsWrongResponseType()
    {
        var packet = BuildInfoPacket("Server", "de_nuke", 5, 10, 0);
        packet[4] = 0x41; // 'A' — challenge, а не info
        Assert.Null(AdvancedA2S.ParseInfo(packet));
    }

    [Fact]
    public void ParseInfo_RejectsStringWithoutTerminator()
    {
        // 0xFFFFFFFF + 'I' + protocol + бесконечная строка без \0
        var bytes = new List<byte> { 0xFF, 0xFF, 0xFF, 0xFF, 0x49, 17 };
        bytes.AddRange(Encoding.UTF8.GetBytes("no terminator here"));
        Assert.Null(AdvancedA2S.ParseInfo(bytes.ToArray()));
    }

    [Fact]
    public void ParseInfo_HandlesGarbageWithoutThrowing()
    {
        var rng = new Random(1234);
        for (var i = 0; i < 200; i++)
        {
            var junk = new byte[rng.Next(0, 64)];
            rng.NextBytes(junk);
            var ex = Record.Exception(() => AdvancedA2S.ParseInfo(junk));
            Assert.Null(ex);
        }
    }

    [Fact]
    public void TryReadNullTerminatedString_AdvancesPastTerminator()
    {
        var data = Encoding.UTF8.GetBytes("ab\0cd\0");
        var index = 0;

        Assert.True(AdvancedA2S.TryReadNullTerminatedString(data, ref index, out var first));
        Assert.Equal("ab", first);
        Assert.True(AdvancedA2S.TryReadNullTerminatedString(data, ref index, out var second));
        Assert.Equal("cd", second);
        Assert.False(AdvancedA2S.TryReadNullTerminatedString(data, ref index, out _));
    }
}
