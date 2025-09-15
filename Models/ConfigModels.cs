using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NotifyMessages;

// ----------------- Конфигурация и модели -------------------
public class Config
{
    public bool? PrintToCenterHtml { get; init; }
    public float? HtmlCenterDuration { get; init; }
    public bool? ShowHtmlWhenDead { get; set; }
    public bool Debug { get; set; } = false;
    public WelcomeMessage? WelcomeMessage { get; init; }

    public string? RestartMessage { get; set; }
    public string? UpdateMessage { get; set; }
    public string? ChangeTeamMessage { get; set; }
    public string? JoinTeamMessage { get; set; }
    public List<Advertisement>? Ads { get; init; }
    public List<string>? Panel { get; init; }
    public string? DefaultLang { get; init; }
    public Dictionary<string, Dictionary<string, string>>? LanguageMessages { get; init; }
    public Dictionary<string, string>? MapsName { get; init; }

    public Dictionary<string, List<string>>? JoinMessages { get; init; }
    public Dictionary<string, List<string>>? LeaveMessages { get; init; }

    public string? TitleAnnounceServers { get; set; }
    public ServerInfo? Servers { get; init; }
}

public class WelcomeMessage
{
    public MessageType MessageType { get; init; }
    public required string Message { get; init; }
    public float DisplayDelay { get; set; } = 2;
}

public class Advertisement
{
    public float Interval { get; init; }
    public List<Dictionary<string, string>> Messages { get; init; } = null!;

    private int _currentMessageIndex;
    [JsonIgnore] public Dictionary<string, string> NextMessages => Messages[_currentMessageIndex++ % Messages.Count];
}

public enum MessageType
{
    Chat = 0,
    Center,
    CenterHtml,
    Console
}

// ── расширено: параметры опроса ────────────────────────────────────────────────
public class ServerInfo
{
    public bool Enabled { get; set; } = false; // включить/выключить анонс серверов
    public float Interval { get; set; } // период опроса
    public int? QueryTimeoutMs { get; set; } // 200–5000 рекомендовано
    public int? CacheTtlSeconds { get; set; } // 0–60
    public List<ServerData> List { get; set; }
}

public class ServerData
{
    public string Ip { get; set; } = ""; // допускается hostname
    public int Port { get; set; }
    public string MessageTemplate { get; set; } = "";
    public string MessageTemplateConsole { get; set; } = "";
    public int? MaxPlayersFallback { get; set; } // на случай OFFLINE
}

// ── структура кеша ─────────────────────────────────────────────────────────────
public sealed class ServerCacheEntry
{
    public string Chat { get; set; } = "";
    public string Console { get; set; } = "";
    public bool Online { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
