using System;
using System.Collections.Generic;
using System.Globalization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;

namespace NotifyMessages;

/// Частичный класс: предпросмотр и проверка шаблонов.
///
/// Смысл обеих команд — сократить петлю «правка конфига → результат» с интервала рекламы
/// (до 7 минут) до секунды. Без этого любые выразительные средства шаблонов невозможно отладить.
public partial class NotifyMessages
{
    private const string PreviewUsage =
        "<welcome | ad <номер> | servers | key <ключ> | raw <текст>>";

    [RequiresPermissions("@css/root")]
    [CommandHelper(minArgs: 1, usage: PreviewUsage)]
    [ConsoleCommand("css_nm_preview", "Показать, как выглядит сообщение из конфига")]
    public void PreviewCommand(CCSPlayerController? controller, CommandInfo command)
    {
        var target = command.GetArg(1);

        switch (target.ToLowerInvariant())
        {
            case "welcome":
                PreviewWelcome(controller);
                break;

            case "ad":
                PreviewAd(controller, command.GetArg(2));
                break;

            case "servers":
                PreviewServers(controller);
                break;

            case "key":
                PreviewKey(controller, command.GetArg(2));
                break;

            case "raw":
                PreviewRaw(controller, command);
                break;

            default:
                Reply(controller, $"[Preview] Использование: css_nm_preview {PreviewUsage}");
                break;
        }
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_nm_check", "Проверить все шаблоны конфигурации")]
    public void CheckConfigCommand(CCSPlayerController? controller, CommandInfo command)
    {
        var issues = ConfigService.CollectIssues(Config);

        if (issues.Count == 0)
        {
            Reply(controller, "[Check] Проблем в шаблонах не найдено.");
            controller?.PrintToChat("[NotifyMessages] Шаблоны в порядке.");
            return;
        }

        var errors = 0;
        Reply(controller, "═══ NotifyMessages: проверка шаблонов ═══");

        foreach (var issue in issues)
        {
            if (issue.Severity == TemplateSeverity.Error) errors++;
            Reply(controller, "  " + issue);
        }

        var summary = $"[Check] Всего: {issues.Count.ToString(CultureInfo.InvariantCulture)}, " +
                      $"из них ошибок: {errors.ToString(CultureInfo.InvariantCulture)}";
        Reply(controller, summary);
        controller?.PrintToChat($"[NotifyMessages] {summary}. Подробности в консоли (клавиша ~).");
    }

    // ---- Цели предпросмотра ---------------------------------------------------

    private void PreviewWelcome(CCSPlayerController? controller)
    {
        var welcome = Config.WelcomeMessage;
        if (welcome == null || string.IsNullOrEmpty(welcome.Message))
        {
            Reply(controller, "[Preview] WelcomeMessage не настроен в Settings.json");
            return;
        }

        var template = welcome.Message.Replace("{PLAYERNAME}",
            controller?.PlayerName ?? "TestPlayer", StringComparison.OrdinalIgnoreCase);

        Show(controller, template, welcome.MessageType, "Settings.json → WelcomeMessage",
            ConfigService.PlayerNameOnly);
    }

    private void PreviewAd(CCSPlayerController? controller, string rawIndex)
    {
        var ads = Config.Ads;
        if (ads == null || ads.Count == 0)
        {
            Reply(controller, "[Preview] В Ads.json нет ни одного блока");
            return;
        }

        if (!int.TryParse(rawIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ||
            number < 1 || number > ads.Count)
        {
            Reply(controller, $"[Preview] Укажите номер блока от 1 до " +
                              ads.Count.ToString(CultureInfo.InvariantCulture));
            return;
        }

        var ad = ads[number - 1];
        if (ad.Messages == null || ad.Messages.Count == 0)
        {
            Reply(controller, $"[Preview] Блок #{rawIndex} пуст");
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
                    Reply(controller, $"[Preview] {where}: неизвестный канал «{channel}». " +
                                      "Допустимые: Chat, Center, CenterHtml, Console, Alert");
                    continue;
                }

                Show(controller, template, messageType, where);
            }
        }
    }

    private void PreviewServers(CCSPlayerController? controller)
    {
        if (Config.Servers is not { Enabled: true })
        {
            Reply(controller, "[Preview] Мониторинг серверов выключен: Servers.json → \"Enabled\": true");
            return;
        }

        if (!string.IsNullOrEmpty(Config.TitleAnnounceServers))
            Show(controller, Config.TitleAnnounceServers!, MessageType.Chat,
                "Settings.json → TitleAnnounceServers");

        var snapshot = _serverStatusService.GetSnapshot();
        if (snapshot.Count == 0)
        {
            Reply(controller, "[Preview] Кеш серверов пуст — опрос ещё не завершился");
            return;
        }

        foreach (var entry in snapshot)
            Show(controller, entry.Chat, MessageType.Chat, "Servers.json → MessageTemplate");
    }

    private void PreviewKey(CCSPlayerController? controller, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            Reply(controller, "[Preview] Использование: css_nm_preview key <ключ из Messages.json>");
            return;
        }

        if (Config.LanguageMessages == null || !Config.LanguageMessages.ContainsKey(key))
        {
            Reply(controller, $"[Preview] Ключа «{key}» нет в Messages.json → LanguageMessages");
            return;
        }

        Show(controller, "{" + key + "}", MessageType.Chat, $"Messages.json → {key}");
    }

    private void PreviewRaw(CCSPlayerController? controller, CommandInfo command)
    {
        var parts = new List<string>();
        for (var i = 2; i < command.ArgCount; i++) parts.Add(command.GetArg(i));

        var template = string.Join(' ', parts);
        if (string.IsNullOrWhiteSpace(template))
        {
            Reply(controller, "[Preview] Использование: css_nm_preview raw <текст с тегами>");
            return;
        }

        Show(controller, template, MessageType.Chat, "raw");
    }

    // ---- Общая часть ----------------------------------------------------------

    /// Рендерит шаблон ТЕМ ЖЕ путём, что и боевые сообщения, и печатает диагностику.
    /// Другой путь ничего бы не доказывал.
    private void Show(CCSPlayerController? controller, string template, MessageType messageType, string where,
        IReadOnlyCollection<string>? contextTags = null)
    {
        if (string.IsNullOrEmpty(template)) return;

        foreach (var issue in TemplateDiagnostics.Analyze(template, Config, where, contextTags))
            Reply(controller, "  " + issue);

        if (controller != null)
        {
            _displayService.Print(messageType, template, controller);
            controller.PrintToConsole($"[Preview] {where} → {messageType}");
            return;
        }

        // Из серверной консоли показать нечего — печатаем текст без управляющих кодов.
        var processed = _messageProcessor.ProcessMessage(template, 0, messageType);
        _logger.Info($"[Preview] {where} → {messageType}: {TextFormatter.StripColorCodes(processed)}");
    }

    private void Reply(CCSPlayerController? controller, string line)
    {
        if (controller is { IsValid: true }) controller.PrintToConsole(line);
        else _logger.Info(line);
    }
}
