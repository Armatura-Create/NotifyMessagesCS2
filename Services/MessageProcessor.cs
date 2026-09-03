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
    private static readonly Regex TagPattern = new Regex(@"\{([^}]*)\}", RegexOptions.Compiled);

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

    /// Применяет локализацию и замену тегов
    public string ProcessMessage(string message, ulong steamId)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        if (_config.LanguageMessages == null)
            return ReplaceMessageTags(message).ReplaceColorTags();

        var matches = TagPattern.Matches(message);
        foreach (Match match in matches)
        {
            var tag = match.Groups[0].Value;
            var tagName = match.Groups[1].Value;

            if (!_config.LanguageMessages.TryGetValue(tagName, out var language))
                continue;

            var isoCode = steamId > 0 ? _getIsoCodeBySteamId(steamId) : _config.DefaultLang;

            if (isoCode != null && language.TryGetValue(isoCode, out var replacement))
                message = message.Replace(tag, replacement);
            else if (_config.DefaultLang != null && language.TryGetValue(_config.DefaultLang, out var defReplacement))
                message = message.Replace(tag, defReplacement);
        }

        return ReplaceMessageTags(message).ReplaceColorTags();
    }

    /// Возвращает случайное сообщение из набора (Join/Leave) с учётом языка игрока
    public string GetRandomLocalizedMessage(Dictionary<string, List<string>>? messages, ulong recipientSteamId,
        string playerName, string country, string city)
    {
        if (messages == null || messages.Count == 0) return string.Empty;

        var lang = _config.DefaultLang ?? "US";
        var iso = _getIsoCodeBySteamId(recipientSteamId);
        if (iso != null && messages.ContainsKey(iso))
            lang = iso;

        if (!messages.TryGetValue(lang, out var messageList) || messageList.Count == 0) return string.Empty;

        var message = messageList[Random.Shared.Next(messageList.Count)];

        return message
            .Replace("{PLAYERNAME}", playerName)
            .Replace("{COUNTRY}", country)
            .Replace("{CITY}", city);
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

        result = result.Replace("\n", "\u2029");

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
