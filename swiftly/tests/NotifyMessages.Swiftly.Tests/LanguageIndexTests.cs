using System.Collections.Generic;
using Xunit;

namespace NotifyMessages.Tests;

/// Язык игрока знает движок (cl_language); IP — это география, а не язык.
public class LanguageIndexTests
{
    private static Config Sample(Dictionary<string, List<string>>? aliases = null) => new()
    {
        DefaultLang = "RU",
        LanguageAliases = aliases,
        LanguageMessages = new Dictionary<string, Dictionary<string, string>>
        {
            ["prefix"] = new() { ["RU"] = "рус", ["US"] = "eng" }
        }
    };

    [Fact]
    public void ClientLanguage_WinsOverGeoIp()
    {
        // Игрок из Германии с английским клиентом должен получить английский
        var index = LanguageIndex.Build(Sample(new Dictionary<string, List<string>>
        {
            ["US"] = new() { "en" }
        }));

        Assert.Equal("US", index.Resolve("en", "DE", "RU"));
    }

    [Fact]
    public void CountryCode_IsUsedWhenClientLanguageIsUnknown()
    {
        var index = LanguageIndex.Build(Sample());

        Assert.Equal("US", index.Resolve(null, "US", "RU"));
    }

    [Fact]
    public void CountryWithoutOwnBlock_FallsBackToAlias()
    {
        // Регрессия: игрок из Казахстана получал DefaultLang, потому что блока "KZ" нет
        var index = LanguageIndex.Build(Sample(new Dictionary<string, List<string>>
        {
            ["RU"] = new() { "ru", "KZ", "BY" }
        }));

        Assert.Equal("RU", index.Resolve(null, "KZ", "US"));
    }

    [Fact]
    public void UnknownEverything_FallsBackToDefault()
    {
        var index = LanguageIndex.Build(Sample());

        Assert.Equal("RU", index.Resolve("zz", "ZZ", "RU"));
    }

    [Fact]
    public void AliasToMissingBlock_IsIgnored()
    {
        // Алиас на блок, которого нет в Messages.json, не должен подменять язык
        var index = LanguageIndex.Build(Sample(new Dictionary<string, List<string>>
        {
            ["FR"] = new() { "fr" }
        }));

        Assert.Equal("RU", index.Resolve("fr", null, "RU"));
    }

    [Fact]
    public void LanguageCase_DoesNotMatter()
    {
        // Движок отдаёт "ru", в конфиге исторически "RU" — вернуться должно написание из конфига
        Assert.Equal("RU", LanguageIndex.Build(Sample()).Resolve("ru", null, "US"));
    }

    [Fact]
    public void DefaultConfig_MapsClientLanguagesToItsBlocks()
    {
        var index = LanguageIndex.Build(ConfigService.BuildDefaultConfig());

        Assert.Equal("US", index.Resolve("en", null, "RU"));
        Assert.Equal("UA", index.Resolve("uk", null, "RU"));
        Assert.Equal("DE", index.Resolve("de", null, "RU"));
        Assert.Equal("RU", index.Resolve(null, "KZ", "US"));
    }
}
