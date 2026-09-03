using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace NotifyMessages;

// ----------------- Главный конфиг (объединяет все части) -------------------
public class Config
{
    // Основные настройки из Settings.json
    public bool Debug { get; set; }
    public string? DefaultLang { get; init; }
    public bool? PrintToCenterHtml { get; init; }
    public bool? ShowHtmlWhenDead { get; set; }
    public float? HtmlCenterDuration { get; init; }
    public WelcomeMessage? WelcomeMessage { get; init; }
    public string? RestartMessage { get; set; }
    public string? UpdateMessage { get; set; }
    public string? ChangeTeamMessage { get; set; }
    public string? JoinTeamMessage { get; set; }
    public string? TitleAnnounceServers { get; set; }
    public RestartNotifyConfig? RestartNotify { get; set; }
    public Dictionary<string, string>? MapsName { get; init; }

    // Сообщения из Messages.json
    public Dictionary<string, Dictionary<string, string>>? LanguageMessages { get; init; }
    public Dictionary<string, List<string>>? JoinMessages { get; init; }
    public Dictionary<string, List<string>>? LeaveMessages { get; init; }

    // Реклама из Ads.json
    public List<Advertisement>? Ads { get; init; }

    // Серверы из Servers.json
    public ServerInfo? Servers { get; init; }
}

// ----------------- Settings.json -------------------
public class SettingsConfig
{
    public bool Debug { get; set; }
    public string DefaultLang { get; set; } = "RU";
    public bool? PrintToCenterHtml { get; set; }
    public bool? ShowHtmlWhenDead { get; set; }
    public float? HtmlCenterDuration { get; set; }
    public WelcomeMessage? WelcomeMessage { get; set; }
    public string? RestartMessage { get; set; }
    public string? UpdateMessage { get; set; }
    public string? ChangeTeamMessage { get; set; }
    public string? JoinTeamMessage { get; set; }
    public string? TitleAnnounceServers { get; set; }
    public RestartNotifyConfig? RestartNotify { get; set; }
    public Dictionary<string, string>? MapsName { get; set; }
}

/// Оповещение игроков о предстоящем рестарте/обновлении.
/// Вызывается извне командой `css_restart_notify <секунды>` — например, апдейтером,
/// который сейчас шлёт в консоль голый `say`.
public class RestartNotifyConfig
{
    public bool Enabled { get; set; } = true;

    /// Куда выводить: 0=Chat, 1=Center, 2=CenterHtml, 3=Console, 4=Alert
    public MessageType MessageType { get; set; } = MessageType.Chat;

    /// Шаблон для секунд, которых нет в Thresholds. Поддерживает {SECONDS} и {TIME_RESTART}
    public string DefaultMessage { get; set; } = "{prefix}{RED}{restart_in_seconds}";

    /// Точные отсечки: количество секунд -> шаблон сообщения.
    /// Ключ строкой, потому что JSON-объект не умеет числовые ключи.
    public Dictionary<string, string> Thresholds { get; set; } = new();

    /// Шаблон для указанной отсечки: точное совпадение, иначе DefaultMessage.
    /// Осознанно без «ближайшего» порога — на 4 секундах показать «через 5 секунд»
    /// или «сервер перезапускается» одинаково неверно, честнее общий шаблон с {SECONDS}.
    public string? ResolveTemplate(int seconds)
    {
        if (Thresholds != null &&
            Thresholds.TryGetValue(seconds.ToString(CultureInfo.InvariantCulture), out var template) &&
            !string.IsNullOrEmpty(template))
        {
            return template;
        }

        return string.IsNullOrEmpty(DefaultMessage) ? null : DefaultMessage;
    }
}

// ----------------- Messages.json -------------------
public class MessagesConfig
{
    public Dictionary<string, Dictionary<string, string>> LanguageMessages { get; set; } = new();
    public Dictionary<string, List<string>>? JoinMessages { get; set; }
    public Dictionary<string, List<string>>? LeaveMessages { get; set; }
}

// ----------------- Ads.json -------------------
public class AdsConfig
{
    public List<Advertisement> Ads { get; set; } = new();
}

// ----------------- Servers.json -------------------
public class ServersConfig
{
    public bool Enabled { get; set; }
    public float Interval { get; set; } = 60;
    public int? QueryTimeoutMs { get; set; } = 500;
    public int? CacheTtlSeconds { get; set; } = 30;
    public List<ServerData> List { get; set; } = new();
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
    public List<Dictionary<string, string>> Messages { get; init; } = new();

    private int _currentMessageIndex;

    /// Следующий набор сообщений блока по кругу, либо null если блок пустой.
    /// Индекс сбрасывается вручную: раньше он рос без границ и после переполнения int
    /// давал отрицательный остаток -> IndexOutOfRange.
    [JsonIgnore]
    public Dictionary<string, string>? NextMessages
    {
        get
        {
            if (Messages == null || Messages.Count == 0) return null;

            if (_currentMessageIndex >= Messages.Count) _currentMessageIndex = 0;
            return Messages[_currentMessageIndex++];
        }
    }
}

public enum MessageType
{
    Chat = 0,
    Center,
    CenterHtml,
    Console,
    Alert
}

// ── Alias для обратной совместимости внутри кода ──────────────────────────────
public class ServerInfo : ServersConfig
{
    // Этот класс используется внутри Config, наследуется от ServersConfig
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
