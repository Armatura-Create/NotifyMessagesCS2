using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace NotifyMessages.Tests;

internal sealed class RecordingLogger : ILogger
{
    public List<string> Infos { get; } = new();
    public List<string> Errors { get; } = new();

    public void Info(string message) => Infos.Add(message);
    public void Debug(string message) { }
    public void Error(string message, Exception? ex = null) => Errors.Add(message + (ex == null ? "" : " | " + ex.Message));
}

/// Кривой конфиг — самая частая проблема у пользователей.
/// Он обязан приводить к внятному сообщению, а не к падению плагина.
public sealed class ConfigResilienceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nm-tests-" + Guid.NewGuid().ToString("N"));

    private string ConfigDir => Path.Combine(_root, "configs/plugins/NotifyMessages");

    private void WriteConfig(string fileName, string content)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(Path.Combine(ConfigDir, fileName), content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void FirstRun_CreatesAllFourConfigsAndReadme()
    {
        var config = new ConfigService(new RecordingLogger()).LoadOrCreate(_root);

        Assert.NotNull(config);
        foreach (var name in new[] { "Settings.json", "Messages.json", "Ads.json", "Servers.json", "README.txt" })
            Assert.True(File.Exists(Path.Combine(ConfigDir, name)), $"{name} не создан");
    }

    [Fact]
    public void BrokenJson_DoesNotThrowAndReportsFileLineAndPosition()
    {
        // Пропущена запятая между полями — ошибка на третьей строке
        WriteConfig("Settings.json", "{\n  \"Debug\": true\n  \"DefaultLang\": \"RU\"\n}");
        WriteConfig("Messages.json", "{ \"LanguageMessages\": {} }");

        var logger = new RecordingLogger();
        var config = new ConfigService(logger).LoadOrCreate(_root);

        Assert.NotNull(config);

        var error = Assert.Single(logger.Errors, e => e.Contains("Settings.json"));
        Assert.Contains("строка", error);
        Assert.Contains("позиция", error);
        Assert.Contains(ConfigDir, error);

        // и громкая сводка, чтобы это не потерялось в логе
        Assert.Contains(logger.Infos, i => i.Contains("не удалось прочитать"));
    }

    [Fact]
    public void BrokenJson_FallsBackToDefaultsForThatFileOnly()
    {
        WriteConfig("Settings.json", "{ это не json ");
        WriteConfig("Ads.json", "{ \"Ads\": [ { \"Interval\": 42, \"Messages\": [ { \"Chat\": \"hi\" } ] } ] }");

        var config = new ConfigService(new RecordingLogger()).LoadOrCreate(_root);

        // Settings уехал на дефолты...
        Assert.Equal("RU", config.DefaultLang);
        // ...а исправный Ads.json прочитан
        Assert.NotNull(config.Ads);
        Assert.Equal(42, config.Ads!.Single().Interval);
    }

    [Fact]
    public void EmptyFile_IsReportedAndDoesNotThrow()
    {
        WriteConfig("Servers.json", "   ");
        WriteConfig("Settings.json", "{ \"DefaultLang\": \"US\" }");

        var logger = new RecordingLogger();
        var config = new ConfigService(logger).LoadOrCreate(_root);

        Assert.Equal("US", config.DefaultLang);
        Assert.Contains(logger.Errors, e => e.Contains("Servers.json") && e.Contains("пуст"));
    }

    [Fact]
    public void TrailingCommasAndCommentsAreTolerated()
    {
        // Осознанное послабление: это самые частые «ошибки» в конфигах, править их руками
        // пользователю незачем, а данные читаются однозначно
        WriteConfig("Settings.json", "{\n  // язык по умолчанию\n  \"DefaultLang\": \"DE\",\n}");

        var logger = new RecordingLogger();
        var config = new ConfigService(logger).LoadOrCreate(_root);

        Assert.Equal("DE", config.DefaultLang);
        Assert.DoesNotContain(logger.Errors, e => e.Contains("Settings.json"));
    }

    [Fact]
    public void BrokenConfig_IsNotOverwritten()
    {
        const string broken = "{ \"DefaultLang\": \"RU\" ";
        WriteConfig("Settings.json", broken);

        new ConfigService(new RecordingLogger()).LoadOrCreate(_root);

        // Плагин не имеет права затирать файл, который пользователь ещё чинит
        Assert.Equal(broken, File.ReadAllText(Path.Combine(ConfigDir, "Settings.json")));
    }
}

/// Регрессия: DisplayService дёргал Server.MaxPlayers в конструкторе, а он создаётся
/// в Load(), где нативы ещё не готовы -> "Global Variables not initialized yet".
public class ServiceConstructionTests
{
    [Fact]
    public void Services_CanBeConstructedWithoutTheGameEngine()
    {
        var config = new Config();
        var logger = new RecordingLogger();
        var processor = new MessageProcessor(config, _ => null);

        var ex = Record.Exception(() =>
        {
            _ = new DisplayService(config, processor, logger);
            _ = new SessionService();
            _ = new ServerStatusService(config, logger, (_, _, _) => null!);
        });

        Assert.Null(ex);
    }
}
