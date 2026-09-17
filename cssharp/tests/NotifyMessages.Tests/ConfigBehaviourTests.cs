using System.Collections.Generic;
using Xunit;

namespace NotifyMessages.Tests;

public class AdvertisementRotationTests
{
    [Fact]
    public void NextMessages_CyclesThroughBlockAndWrapsAround()
    {
        var ad = new Advertisement
        {
            Messages = new List<Dictionary<string, string>>
            {
                new() { ["Chat"] = "first" },
                new() { ["Chat"] = "second" }
            }
        };

        Assert.Equal("first", ad.NextMessages!["Chat"]);
        Assert.Equal("second", ad.NextMessages!["Chat"]);
        Assert.Equal("first", ad.NextMessages!["Chat"]);
    }

    [Fact]
    public void NextMessages_ReturnsNullForEmptyBlock()
    {
        // Регрессия: пустой блок в Ads.json ронял таймер делением на ноль
        var ad = new Advertisement { Messages = new List<Dictionary<string, string>>() };
        Assert.Null(ad.NextMessages);
    }
}

public class RestartNotifyConfigTests
{
    private static RestartNotifyConfig Sample() => new()
    {
        DefaultMessage = "default {SECONDS}",
        Thresholds = new Dictionary<string, string>
        {
            ["300"] = "five minutes",
            ["1"] = "restarting"
        }
    };

    [Theory]
    [InlineData(300, "five minutes")]
    [InlineData(1, "restarting")]
    public void ResolveTemplate_PrefersExactThreshold(int seconds, string expected)
    {
        Assert.Equal(expected, Sample().ResolveTemplate(seconds));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(999)]
    [InlineData(0)]
    public void ResolveTemplate_FallsBackToDefault(int seconds)
    {
        Assert.Equal("default {SECONDS}", Sample().ResolveTemplate(seconds));
    }

    [Fact]
    public void ResolveTemplate_ReturnsNullWhenNothingConfigured()
    {
        var config = new RestartNotifyConfig { DefaultMessage = "", Thresholds = new Dictionary<string, string>() };
        Assert.Null(config.ResolveTemplate(60));
    }
}

public class ServerStatusLineTests
{
    [Fact]
    public void BuildServerLines_FillsPlaceholdersAndSubtractsBots()
    {
        var data = new ServerData
        {
            Ip = "10.0.0.1",
            Port = 27015,
            MessageTemplate = "{SERVER_IP}:{SERVER_PORT} {SERVER_MAP} {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}"
        };
        var info = new A2SInfoResponse { Map = "de_dust2", Players = 10, Bots = 4, MaxPlayers = 32 };

        var (chat, console) = ServerStatusService.BuildServerLines(data, info);

        Assert.Equal("10.0.0.1:27015 de_dust2 6/32", chat);
        Assert.Equal(chat, console); // console-шаблон не задан — берётся chat
    }

    [Fact]
    public void BuildServerLines_MarksOfflineServerAndUsesFallbackSlots()
    {
        var data = new ServerData
        {
            Ip = "10.0.0.2",
            Port = 27016,
            MessageTemplate = "{SERVER_MAP} {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
            MaxPlayersFallback = 64
        };

        var (chat, _) = ServerStatusService.BuildServerLines(data, null);

        Assert.Equal("OFFLINE 0/64", chat);
    }
}
