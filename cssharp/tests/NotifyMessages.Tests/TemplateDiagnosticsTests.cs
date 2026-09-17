using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NotifyMessages.Tests;

public class TemplateDiagnosticsTests
{
    private static Config Sample() => new()
    {
        DefaultLang = "RU",
        LanguageMessages = new Dictionary<string, Dictionary<string, string>>
        {
            ["prefix"] = new() { ["RU"] = "{LIGHTBLUE}Server ➡{DEFAULT} ", ["US"] = "{LIGHTBLUE}Server ➡{DEFAULT} " },
            ["hello"] = new() { ["RU"] = "Привет", ["US"] = "Hello" },
            ["broken"] = new() { ["RU"] = "Текст с {opechatka}", ["US"] = "Text with {opechatka}" }
        }
    };

    [Fact]
    public void UnknownTag_IsReportedAsError()
    {
        var issues = TemplateDiagnostics.Analyze("{prefix}{reklama_9}", Sample(), "Ads.json");

        var issue = Assert.Single(issues);
        Assert.Equal(TemplateSeverity.Error, issue.Severity);
        Assert.Equal("{reklama_9}", issue.Tag);
    }

    [Fact]
    public void ColorAndSystemTags_AreNotFlagged()
    {
        // {SPACE} обрабатывает ReplaceColorTags, {MAP}/{PLAYERS} — ReplaceMessageTags
        var issues = TemplateDiagnostics.Analyze(
            "{RED}{SPACE}{MAP} {PLAYERS}/{MAXPLAYERS} {SERVERNAME}{DEFAULT}", Sample(), "test");

        Assert.Empty(issues);
    }

    [Fact]
    public void ColorTagCase_IsIgnored()
    {
        Assert.Empty(TemplateDiagnostics.Analyze("{red}{Default}", Sample(), "test"));
    }

    [Fact]
    public void ContextTag_OutsideItsPlace_IsWarning()
    {
        // {SECONDS} подставляет только css_restart_notify — в рекламе он останется текстом
        var issues = TemplateDiagnostics.Analyze("{SECONDS}", Sample(), "Ads.json");

        var issue = Assert.Single(issues);
        Assert.Equal(TemplateSeverity.Warning, issue.Severity);
        Assert.Contains("RestartNotify", issue.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ContextTag_WhereAllowed_IsSilent()
    {
        var issues = TemplateDiagnostics.Analyze("{SECONDS}", Sample(), "RestartNotify",
            new[] { "{SECONDS}" });

        Assert.Empty(issues);
    }

    [Fact]
    public void NestedTagInsideTranslation_IsReported()
    {
        // Опечатка спрятана внутри текста из Messages.json, а не в самом шаблоне
        var issues = TemplateDiagnostics.Analyze("{broken}", Sample(), "Settings.json");

        Assert.NotEmpty(issues);
        Assert.All(issues, i => Assert.Equal("{opechatka}", i.Tag));
        Assert.Contains(issues, i => i.Where.Contains("{broken}[RU]", System.StringComparison.Ordinal));
    }

    [Fact]
    public void CyclicKeys_DoNotHang()
    {
        var config = new Config
        {
            DefaultLang = "RU",
            LanguageMessages = new Dictionary<string, Dictionary<string, string>>
            {
                ["a"] = new() { ["RU"] = "{b}" },
                ["b"] = new() { ["RU"] = "{a}" }
            }
        };

        Assert.Empty(TemplateDiagnostics.Analyze("{a}", config, "test"));
    }

    [Fact]
    public void MissingTranslation_IsWarning()
    {
        var config = new Config
        {
            DefaultLang = "RU",
            LanguageMessages = new Dictionary<string, Dictionary<string, string>>
            {
                ["full"] = new() { ["RU"] = "раз", ["US"] = "one" },
                ["partial"] = new() { ["RU"] = "два" }
            }
        };

        var issues = TemplateDiagnostics.AnalyzeLanguageCoverage(config);

        var issue = Assert.Single(issues);
        Assert.Equal(TemplateSeverity.Warning, issue.Severity);
        Assert.Equal("{partial}", issue.Tag);
        Assert.Contains("US", issue.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultConfig_HasNoTemplateErrors()
    {
        // Регрессия: первый запуск не имеет права показать игроку тег в фигурных скобках
        var issues = ConfigService.CollectIssues(ConfigService.BuildDefaultConfig());

        var errors = issues.Where(i => i.Severity == TemplateSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }
}
