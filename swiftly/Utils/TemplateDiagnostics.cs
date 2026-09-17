using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NotifyMessages;

internal enum TemplateSeverity
{
    Error,
    Warning
}

/// Одна претензия к шаблону: что не так и где именно.
internal sealed record TemplateIssue(TemplateSeverity Severity, string Where, string Tag, string Text)
{
    public override string ToString()
    {
        var mark = Severity == TemplateSeverity.Error ? "ОШИБКА" : "внимание";
        return $"[{mark}] {Where}: {Text}";
    }
}

/// Статический анализатор шаблонов сообщений.
///
/// Смысл: тег, который не сможет подставить ни MessageProcessor, ни TextFormatter,
/// доезжает до игрока литеральным текстом в фигурных скобках. Раньше это находил
/// только игрок. Здесь это находит машина — при загрузке конфига и по команде.
///
/// Нативов внутри нет вообще: класс чистый и покрыт юнит-тестами.
internal static class TemplateDiagnostics
{
    // Больше трёх уровней вложенности ({prefix} -> текст -> ещё ключ) не бывает,
    // а ограничение заодно страхует от циклов вида {a} -> {b} -> {a}.
    private const int MaxDepth = 3;

    /// Теги, работающие лишь в отдельных местах конфига: значение подставляет вызывающий код,
    /// а не MessageProcessor. Значение словаря — человекочитаемое «где именно».
    internal static readonly IReadOnlyDictionary<string, string> ContextualTags =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{PLAYERNAME}"] = "WelcomeMessage, ChangeTeamMessage, JoinTeamMessage, Join/LeaveMessages",
            ["{TEAM}"] = "ChangeTeamMessage, JoinTeamMessage",
            ["{OLD_TEAM}"] = "ChangeTeamMessage",
            ["{TIME_RESTART}"] = "RestartNotify",
            ["{SECONDS}"] = "RestartNotify",
            ["{COUNTRY}"] = "JoinMessages, LeaveMessages",
            ["{CITY}"] = "JoinMessages, LeaveMessages",
            ["{SERVER_IP}"] = "Servers.json",
            ["{SERVER_PORT}"] = "Servers.json",
            ["{SERVER_MAP}"] = "Servers.json",
            ["{SERVER_PLAYERS}"] = "Servers.json",
            ["{SERVER_MAXPLAYERS}"] = "Servers.json"
        };

    /// Разбирает один шаблон. allowedContextTags — теги, которые в этом месте конфига
    /// действительно кто-то подставит (см. ContextualTags).
    public static IReadOnlyList<TemplateIssue> Analyze(
        string? template,
        Config config,
        string where,
        IReadOnlyCollection<string>? allowedContextTags = null)
    {
        var issues = new List<TemplateIssue>();
        if (string.IsNullOrEmpty(template)) return issues;

        var allowed = allowedContextTags == null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(allowedContextTags, StringComparer.OrdinalIgnoreCase);

        Walk(template, config, where, allowed, issues,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);

        return issues;
    }

    /// Полнота переводов: ключ есть, но не на всех языках, которые встречаются в Messages.json.
    /// Отдельным проходом, а не внутри Walk: иначе одна и та же претензия повторялась бы
    /// столько раз, сколько шаблонов ссылается на ключ.
    public static IReadOnlyList<TemplateIssue> AnalyzeLanguageCoverage(Config config)
    {
        var issues = new List<TemplateIssue>();
        var languages = CollectLanguages(config);
        if (config.LanguageMessages == null || languages.Count == 0) return issues;

        foreach (var (key, translations) in config.LanguageMessages)
        {
            if (translations == null) continue;

            foreach (var lang in languages)
            {
                if (translations.ContainsKey(lang)) continue;

                issues.Add(new TemplateIssue(
                    TemplateSeverity.Warning,
                    $"Messages.json → {key}",
                    "{" + key + "}",
                    $"нет перевода на «{lang}» — игрок с этим языком увидит текст на другом языке"));
            }
        }

        return issues;
    }

    /// Все языки, встречающиеся в конфиге: объединение ключей переводов плюс DefaultLang.
    internal static IReadOnlyList<string> CollectLanguages(Config config)
    {
        var languages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(config.DefaultLang)) languages.Add(config.DefaultLang);

        if (config.LanguageMessages != null)
        {
            foreach (var translations in config.LanguageMessages.Values)
            {
                if (translations == null) continue;
                foreach (var lang in translations.Keys) languages.Add(lang);
            }
        }

        var result = new List<string>(languages.Count);
        result.AddRange(languages);
        return result;
    }

    private static void Walk(
        string text,
        Config config,
        string where,
        HashSet<string> allowedContextTags,
        List<TemplateIssue> issues,
        HashSet<string> expandedKeys,
        int depth)
    {
        if (depth > MaxDepth) return;

        foreach (Match match in MessageProcessor.TagPattern.Matches(text))
        {
            var tag = match.Groups[0].Value;
            var name = match.Groups[1].Value;

            if (TextFormatter.KnownColorTags.Contains(tag)) continue;
            if (MessageProcessor.IsSystemTag(tag)) continue;
            if (allowedContextTags.Contains(tag)) continue;

            if (ContextualTags.TryGetValue(tag, out var worksIn))
            {
                issues.Add(new TemplateIssue(TemplateSeverity.Warning, where, tag,
                    $"тег {tag} здесь никто не подставит — он работает только в: {worksIn}"));
                continue;
            }

            if (config.LanguageMessages != null &&
                config.LanguageMessages.TryGetValue(name, out var translations))
            {
                // Ключ известен. Его тексты сами содержат теги — проверяем и их.
                if (translations == null || !expandedKeys.Add(name)) continue;

                foreach (var (lang, value) in translations)
                {
                    if (string.IsNullOrEmpty(value)) continue;
                    Walk(value, config, $"{where} → {tag}[{lang}]", allowedContextTags, issues,
                        expandedKeys, depth + 1);
                }

                continue;
            }

            issues.Add(new TemplateIssue(TemplateSeverity.Error, where, tag,
                $"неизвестный тег {tag} — игрок увидит его как текст. " +
                "Добавьте ключ в Messages.json или исправьте опечатку"));
        }
    }
}
