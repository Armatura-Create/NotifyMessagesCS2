using System.Collections.Generic;
using System.Globalization;

namespace NotifyMessages;

/// Обход всех шаблонов конфигурации через TemplateDiagnostics.
///
/// Вынесен в отдельный partial-файл: это перечисление секций конфига, а не логика загрузки.
/// Используется дважды — при загрузке (ValidateConfig) и командой css_nm_check.
public sealed partial class ConfigService
{
    // Наборы контекстных тегов по местам конфига: их подставляет вызывающий код,
    // и только там они имеют смысл.
    internal static readonly string[] PlayerNameOnly = { "{PLAYERNAME}" };
    internal static readonly string[] RestartTags = { "{TIME_RESTART}", "{SECONDS}" };
    internal static readonly string[] ChangeTeamTags = { "{PLAYERNAME}", "{TEAM}", "{OLD_TEAM}" };
    internal static readonly string[] JoinTeamTags = { "{PLAYERNAME}", "{TEAM}" };
    internal static readonly string[] JoinLeaveTags = { "{PLAYERNAME}", "{COUNTRY}", "{CITY}" };

    internal static readonly string[] ServerTags =
    {
        "{SERVER_IP}", "{SERVER_PORT}", "{SERVER_MAP}", "{SERVER_PLAYERS}", "{SERVER_MAXPLAYERS}"
    };

    /// Все претензии к шаблонам конфига: неизвестные теги, теги не в своём контексте,
    /// пробелы в переводах.
    internal static IReadOnlyList<TemplateIssue> CollectIssues(Config config)
    {
        var issues = new List<TemplateIssue>();

        void Check(string? template, string where, string[]? context = null)
        {
            if (string.IsNullOrEmpty(template)) return;
            issues.AddRange(TemplateDiagnostics.Analyze(template, config, where, context));
        }

        // --- Settings.json ---
        Check(config.WelcomeMessage?.Message, "Settings.json → WelcomeMessage", PlayerNameOnly);
        Check(config.ChangeTeamMessage, "Settings.json → ChangeTeamMessage", ChangeTeamTags);
        Check(config.JoinTeamMessage, "Settings.json → JoinTeamMessage", JoinTeamTags);
        Check(config.TitleAnnounceServers, "Settings.json → TitleAnnounceServers");

        var notify = config.RestartNotify;
        if (notify != null)
        {
            Check(notify.DefaultMessage, "Settings.json → RestartNotify.DefaultMessage", RestartTags);

            if (notify.Thresholds != null)
            {
                foreach (var (seconds, template) in notify.Thresholds)
                    Check(template, $"Settings.json → RestartNotify.Thresholds[{seconds}]", RestartTags);
            }
        }

        // --- Messages.json ---
        CheckMessageList(config.JoinMessages, "Messages.json → JoinMessages", config, issues);
        CheckMessageList(config.LeaveMessages, "Messages.json → LeaveMessages", config, issues);
        issues.AddRange(TemplateDiagnostics.AnalyzeLanguageCoverage(config));

        // --- Ads.json ---
        if (config.Ads != null)
        {
            for (var i = 0; i < config.Ads.Count; i++)
            {
                var ad = config.Ads[i];
                if (ad?.Messages == null) continue;

                for (var m = 0; m < ad.Messages.Count; m++)
                {
                    var block = ad.Messages[m];
                    if (block == null) continue;

                    foreach (var (channel, template) in block)
                    {
                        var index = (i + 1).ToString(CultureInfo.InvariantCulture);
                        Check(template, $"Ads.json → блок #{index}, {channel}");
                    }
                }
            }
        }

        // --- Servers.json ---
        var servers = config.Servers?.List;
        if (servers != null)
        {
            for (var i = 0; i < servers.Count; i++)
            {
                var server = servers[i];
                if (server == null) continue;

                var index = (i + 1).ToString(CultureInfo.InvariantCulture);
                Check(server.MessageTemplate, $"Servers.json → сервер #{index}, MessageTemplate", ServerTags);
                Check(server.MessageTemplateConsole, $"Servers.json → сервер #{index}, MessageTemplateConsole",
                    ServerTags);
            }
        }

        return issues;
    }

    private static void CheckMessageList(Dictionary<string, List<string>>? messages, string where, Config config,
        List<TemplateIssue> issues)
    {
        if (messages == null) return;

        foreach (var (lang, list) in messages)
        {
            if (list == null) continue;

            for (var i = 0; i < list.Count; i++)
            {
                var index = (i + 1).ToString(CultureInfo.InvariantCulture);
                issues.AddRange(TemplateDiagnostics.Analyze(list[i], config, $"{where}[{lang}] #{index}",
                    JoinLeaveTags));
            }
        }
    }
}
