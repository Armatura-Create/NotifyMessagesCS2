using System;
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
/// - замену \\n на спец-символ для отображения в центре
/// - замену имён карт согласно Config.MapsName
public sealed class MessageProcessor
{
    private readonly Config _config;
    private readonly Func<ulong, string?> _getIsoCodeBySteamId;

    public MessageProcessor(Config config, Func<ulong, string?> getIsoCodeBySteamId)
    {
        _config = config;
        _getIsoCodeBySteamId = getIsoCodeBySteamId;
    }

    /// Применяет локализацию и замену тегов
    public string ProcessMessage(string message, ulong steamId)
    {
        if (_config.LanguageMessages == null)
            return ReplaceMessageTags(message).ReplaceColorTags();

        var matches = Regex.Matches(message, @"\{([^}]*)\}");
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

    /// Заменяет системные теги и имена карт
    public string ReplaceMessageTags(string message)
    {
        var mapName = NativeAPI.GetMapName();

        var replacedMessage = message
            .Replace("{MAP}", mapName)
            .Replace("{TIME}", DateTime.Now.ToString("HH:mm:ss"))
            .Replace("{DATE}", DateTime.Now.ToString("dd.MM.yyyy"))
            .Replace("{SERVERNAME}", ConVar.Find("hostname")?.StringValue ?? "Server")
            .Replace("{IP}", ConVar.Find("ip")?.StringValue ?? "127.0.0.1")
            .Replace("{PORT}", ConVar.Find("hostport")?.GetPrimitiveValue<int>().ToString() ?? "27015")
            .Replace("{MAXPLAYERS}", Server.MaxPlayers.ToString())
            .Replace("{PLAYERS}", CounterStrikeSharp.API.Utilities.GetPlayers().Count(u => u.PlayerPawn?.Value?.IsValid == true).ToString())
            .Replace("\n", "\u2029");

        if (_config.MapsName != null)
        {
            foreach (var (key, niceName) in _config.MapsName)
            {
                replacedMessage = Regex.Replace(replacedMessage, $@"\b{Regex.Escape(key)}\b", niceName);
            }
        }

        return replacedMessage;
    }
}
