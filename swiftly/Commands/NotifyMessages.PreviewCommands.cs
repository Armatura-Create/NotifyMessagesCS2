using System;
using System.Collections.Generic;
using System.Globalization;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;

namespace NotifyMessages;

/// Частичный класс: предпросмотр и проверка шаблонов.
///
/// Смысл обеих команд — сократить петлю «правка конфига → результат» с интервала рекламы
/// (до 7 минут) до секунды. Без этого любые выразительные средства шаблонов невозможно отладить.
public sealed partial class NotifyMessages
{
    private const string PreviewUsage =
        "<welcome | ad <номер> | servers | key <ключ> | raw <текст>>";

    private void OnPreviewCommand(ICommandContext context)
    {
        var args = context.Args;
        var player = context.IsSentByPlayer ? context.Sender : null;
        var target = args.Length > 0 ? args[0] : "";

        switch (target.ToLowerInvariant())
        {
            case "welcome":
                PreviewWelcome(player);
                break;

            case "ad":
                PreviewAd(player, args.Length > 1 ? args[1] : "");
                break;

            case "servers":
                PreviewServers(player);
                break;

            case "key":
                PreviewKey(player, args.Length > 1 ? args[1] : "");
                break;

            case "raw":
                PreviewRaw(player, args.Length > 1 ? string.Join(' ', args, 1, args.Length - 1) : "");
                break;

            default:
                Reply(player, $"[Preview] Использование: sw_nm_preview {PreviewUsage}");
                break;
        }
    }

    private void OnCheckCommand(ICommandContext context)
    {
        var player = context.IsSentByPlayer ? context.Sender : null;
        var issues = ConfigService.CollectIssues(Config);

        if (issues.Count == 0)
        {
            Reply(player, "[Check] Проблем в шаблонах не найдено.");
            if (player != null) context.Reply("[NotifyMessages] Шаблоны в порядке.");
            return;
        }

        var errors = 0;
        Reply(player, "═══ NotifyMessages: проверка шаблонов ═══");

        foreach (var issue in issues)
        {
            if (issue.Severity == TemplateSeverity.Error) errors++;
            Reply(player, "  " + issue);
        }

        var summary = $"[Check] Всего: {issues.Count.ToString(CultureInfo.InvariantCulture)}, " +
                      $"из них ошибок: {errors.ToString(CultureInfo.InvariantCulture)}";
        Reply(player, summary);
        if (player != null) context.Reply($"[NotifyMessages] {summary}. Подробности в консоли (клавиша ~).");
    }

    // ---- Цели предпросмотра ---------------------------------------------------

    private void PreviewWelcome(IPlayer? player)
    {
        var welcome = Config.WelcomeMessage;
        if (welcome == null || string.IsNullOrEmpty(welcome.Message))
        {
            Reply(player, "[Preview] WelcomeMessage не настроен в Settings.json");
            return;
        }

        var template = welcome.Message.Replace("{PLAYERNAME}",
            SafeName(player) ?? "TestPlayer", StringComparison.OrdinalIgnoreCase);

        Show(player, template, welcome.MessageType, "Settings.json → WelcomeMessage",
            ConfigService.PlayerNameOnly);
    }

    private void PreviewAd(IPlayer? player, string rawIndex)
    {
        var ads = Config.Ads;
        if (ads == null || ads.Count == 0)
        {
            Reply(player, "[Preview] В Ads.json нет ни одного блока");
            return;
        }

        if (!int.TryParse(rawIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ||
            number < 1 || number > ads.Count)
        {
            Reply(player, $"[Preview] Укажите номер блока от 1 до " +
                          ads.Count.ToString(CultureInfo.InvariantCulture));
            return;
        }

        var ad = ads[number - 1];
        if (ad.Messages == null || ad.Messages.Count == 0)
        {
            Reply(player, $"[Preview] Блок #{rawIndex} пуст");
            return;
        }

        // Идём по Messages напрямую, а НЕ через ad.NextMessages: тот сдвигает боевую ротацию.
        for (var i = 0; i < ad.Messages.Count; i++)
        {
            var block = ad.Messages[i];
            if (block == null) continue;

            foreach (var (channel, template) in block)
            {
                var where = $"Ads.json → блок #{rawIndex}, сообщение #{(i + 1).ToString(CultureInfo.InvariantCulture)}, {channel}";

                if (!Enum.TryParse<MessageType>(channel, ignoreCase: true, out var messageType))
                {
                    Reply(player, $"[Preview] {where}: неизвестный канал «{channel}». " +
                                  "Допустимые: Chat, Center, CenterHtml, Console, Alert");
                    continue;
                }

                Show(player, template, messageType, where);
            }
        }
    }

    private void PreviewServers(IPlayer? player)
    {
        if (Config.Servers is not { Enabled: true })
        {
            Reply(player, "[Preview] Мониторинг серверов выключен: Servers.json → \"Enabled\": true");
            return;
        }

        if (!string.IsNullOrEmpty(Config.TitleAnnounceServers))
            Show(player, Config.TitleAnnounceServers!, MessageType.Chat, "Settings.json → TitleAnnounceServers");

        var snapshot = _serverStatusService.GetSnapshot();
        if (snapshot.Count == 0)
        {
            Reply(player, "[Preview] Кеш серверов пуст — опрос ещё не завершился");
            return;
        }

        foreach (var entry in snapshot)
            Show(player, entry.Chat, MessageType.Chat, "Servers.json → MessageTemplate");
    }

    private void PreviewKey(IPlayer? player, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            Reply(player, "[Preview] Использование: sw_nm_preview key <ключ из Messages.json>");
            return;
        }

        if (Config.LanguageMessages == null || !Config.LanguageMessages.ContainsKey(key))
        {
            Reply(player, $"[Preview] Ключа «{key}» нет в Messages.json → LanguageMessages");
            return;
        }

        Show(player, "{" + key + "}", MessageType.Chat, $"Messages.json → {key}");
    }

    private void PreviewRaw(IPlayer? player, string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            Reply(player, "[Preview] Использование: sw_nm_preview raw <текст с тегами>");
            return;
        }

        Show(player, template, MessageType.Chat, "raw");
    }

    // ---- Общая часть ----------------------------------------------------------

    /// Рендерит шаблон ТЕМ ЖЕ путём, что и боевые сообщения, и печатает диагностику.
    /// Другой путь ничего бы не доказывал.
    private void Show(IPlayer? player, string template, MessageType messageType, string where,
        IReadOnlyCollection<string>? contextTags = null)
    {
        if (string.IsNullOrEmpty(template)) return;

        foreach (var issue in TemplateDiagnostics.Analyze(template, Config, where, contextTags))
            Reply(player, "  " + issue);

        if (player != null)
        {
            _displayService.Print(messageType, template, player);
            Reply(player, $"[Preview] {where} → {messageType}");
            return;
        }

        // Из серверной консоли показать нечего — печатаем текст без управляющих кодов.
        var processed = _messageProcessor.ProcessMessage(template, 0, messageType);
        _logger.Info($"[Preview] {where} → {messageType}: {TextFormatter.StripColorCodes(processed)}");
    }

    /// В консоль игрока (клавиша ~) или в консоль сервера.
    private void Reply(IPlayer? player, string line)
    {
        if (player == null)
        {
            _logger.Info(line);
            return;
        }

        try
        {
            player.SendConsole(line);
        }
        catch (Exception)
        {
            // Игрок стал невалидным — строка уходит хотя бы в лог сервера
            _logger.Info(line);
        }
    }

    private static string? SafeName(IPlayer? player)
    {
        if (player == null) return null;

        try
        {
            return player.Name;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
