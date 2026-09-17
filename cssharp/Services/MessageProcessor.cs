using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Server = CounterStrikeSharp.API.Server;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace NotifyMessages;

/// Сервис обработки и локализации сообщений
/// Отвечает за:
/// - подстановку языковых шаблонов из Config.LanguageMessages
/// - замену тегов {MAP}/{TIME}/{DATE}/{SERVERNAME}/{IP}/{PORT}/{MAXPLAYERS}/{PLAYERS}
/// - замену \n на спец-символ для отображения в центре
/// - замену имён карт согласно Config.MapsName
///
/// ВАЖНО: вызывать только из главного потока — ReplaceMessageTags дёргает нативы
/// (NativeAPI.GetMapName, ConVar.Find, Utilities.GetPlayers).
public sealed class MessageProcessor
{
    // internal, а не private: диагностика шаблонов обязана видеть ровно те же теги,
    // что и подстановка, иначе анализатор начнёт врать.
    internal static readonly Regex TagPattern = new Regex(@"\{([^}]*)\}", RegexOptions.Compiled);

    /// Теги, которые ReplaceMessageTags подставляет в любом сообщении, без всякого контекста.
    /// Список — источник истины для диагностики шаблонов, держать синхронно с ReplaceMessageTags.
    internal static readonly string[] SystemTags =
    {
        "{MAP}", "{TIME}", "{DATE}", "{SERVERNAME}", "{IP}", "{PORT}", "{MAXPLAYERS}", "{PLAYERS}"
    };

    private static readonly HashSet<string> SystemTagSet =
        new(SystemTags, StringComparer.OrdinalIgnoreCase);

    internal static bool IsSystemTag(string tag) => SystemTagSet.Contains(tag);

    private readonly Config _config;
    private readonly Func<ulong, string?> _getIsoCodeBySteamId;

    public MessageProcessor(Config config, Func<ulong, string?> getIsoCodeBySteamId)
    {
        _config = config;
        _getIsoCodeBySteamId = getIsoCodeBySteamId;
    }

    /// Получить ISO-код для игрока (для кеширования)
    public string? GetIsoCodeBySteamId(ulong steamId)
    {
        return _getIsoCodeBySteamId(steamId);
    }

    /// Применяет локализацию, контекстные значения, системные теги и рендер под канал.
    ///
    /// Порядок частей не произволен:
    ///   язык -> контекстные значения -> системные теги -> рендер.
    /// Значения подставляются ДО рендера, потому что сами содержат теги ({RED}Terrorists{DEFAULT}).
    /// Раньше их подставляли после ProcessMessage, и игрок видел эти теги текстом.
    public string ProcessMessage(string message, ulong steamId,
        MessageType channel = MessageType.Chat,
        IReadOnlyDictionary<string, string>? values = null)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        var result = ApplyLanguage(message, steamId);
        result = ApplyValues(result, values, channel);
        result = ReplaceMessageTags(result);

        return Render(result, channel);
    }

    private string ApplyLanguage(string message, ulong steamId)
    {
        if (_config.LanguageMessages == null) return message;

        var matches = TagPattern.Matches(message);
        foreach (Match match in matches)
        {
            var tag = match.Groups[0].Value;
            var tagName = match.Groups[1].Value;

            if (!_config.LanguageMessages.TryGetValue(tagName, out var language))
                continue;

            var isoCode = steamId > 0 ? _getIsoCodeBySteamId(steamId) : _config.DefaultLang;

            if (isoCode != null && language.TryGetValue(isoCode, out var replacement))
                message = message.Replace(tag, replacement, StringComparison.Ordinal);
            else if (_config.DefaultLang != null && language.TryGetValue(_config.DefaultLang, out var defReplacement))
                message = message.Replace(tag, defReplacement, StringComparison.Ordinal);
        }

        return message;
    }

    /// Подстановка контекстных значений ({PLAYERNAME}, {TEAM}, {SECONDS}, ...).
    /// Регистр игнорируется: дефолтный Messages.json пишет часть тегов строчными.
    /// Для HTML-панели значения экранируются — ник игрока это недоверенные данные.
    internal static string ApplyValues(string message, IReadOnlyDictionary<string, string>? values,
        MessageType channel)
    {
        if (values == null || values.Count == 0) return message;

        foreach (var (tag, value) in values)
        {
            var safe = channel == MessageType.CenterHtml ? TextFormatter.EscapeHtml(value) : value;
            message = TextFormatter.ReplaceIgnoreCase(message, tag, safe);
        }

        return message;
    }

    /// Финальный рендер. У каналов разные грамматики, одна строка не может быть верной во всех:
    /// чат понимает управляющие байты, HTML-панель — разметку, центр и консоль — только текст.
    internal static string Render(string text, MessageType channel) => channel switch
    {
        MessageType.Chat => text.ReplaceColorTags().Replace("\n", "\u2029", StringComparison.Ordinal),
        MessageType.CenterHtml => TextFormatter.ToCenterHtml(text),
        // В консоли перенос строки — настоящий \n, а не U+2029
        MessageType.Console => TextFormatter.RemoveColorTags(text),
        _ => TextFormatter.RemoveColorTags(text).Replace("\n", "\u2029", StringComparison.Ordinal)
    };

    /// Возвращает случайный ШАБЛОН из набора (Join/Leave) на языке получателя.
    /// Значения ({PLAYERNAME}, {COUNTRY}, {CITY}) подставляет ProcessMessage — единственная
    /// точка подстановки на весь плагин.
    public string GetRandomLocalizedMessage(Dictionary<string, List<string>>? messages, ulong recipientSteamId)
    {
        if (messages == null || messages.Count == 0) return string.Empty;

        var lang = _config.DefaultLang ?? "US";
        var iso = _getIsoCodeBySteamId(recipientSteamId);
        if (iso != null && messages.ContainsKey(iso))
            lang = iso;

        if (!messages.TryGetValue(lang, out var messageList) || messageList.Count == 0) return string.Empty;

        return messageList[Random.Shared.Next(messageList.Count)];
    }

    /// Заменяет системные теги и имена карт.
    /// Каждый плейсхолдер резолвится ТОЛЬКО если реально встречается в строке —
    /// иначе на каждое сообщение уходило по 3 ConVar.Find + GetPlayers() + GetMapName().
    public string ReplaceMessageTags(string message)
    {
        if (string.IsNullOrEmpty(message)) return message;

        var result = message;

        if (result.Contains("{MAP}", StringComparison.Ordinal))
            result = result.Replace("{MAP}", NativeAPI.GetMapName());

        if (result.Contains("{TIME}", StringComparison.Ordinal))
            result = result.Replace("{TIME}", DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture));

        if (result.Contains("{DATE}", StringComparison.Ordinal))
            result = result.Replace("{DATE}", DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));

        if (result.Contains("{SERVERNAME}", StringComparison.Ordinal))
            result = result.Replace("{SERVERNAME}", ConVar.Find("hostname")?.StringValue ?? "Server");

        if (result.Contains("{IP}", StringComparison.Ordinal))
            result = result.Replace("{IP}", ConVar.Find("ip")?.StringValue ?? "127.0.0.1");

        if (result.Contains("{PORT}", StringComparison.Ordinal))
            result = result.Replace("{PORT}",
                ConVar.Find("hostport")?.GetPrimitiveValue<int>().ToString(CultureInfo.InvariantCulture) ?? "27015");

        if (result.Contains("{MAXPLAYERS}", StringComparison.Ordinal))
            result = result.Replace("{MAXPLAYERS}", Server.MaxPlayers.ToString(CultureInfo.InvariantCulture));

        if (result.Contains("{PLAYERS}", StringComparison.Ordinal))
            result = result.Replace("{PLAYERS}",
                Utilities.GetPlayers().Count(u => u.PlayerPawn?.Value?.IsValid == true).ToString(CultureInfo.InvariantCulture));

        if (_config.MapsName != null)
        {
            foreach (var (key, niceName) in _config.MapsName)
            {
                // Regex дорогой — не запускаем его для карт, которых нет в строке
                if (result.Contains(key, StringComparison.Ordinal))
                    result = Regex.Replace(result, $@"\b{Regex.Escape(key)}\b", niceName);
            }
        }

        return result;
    }
}
