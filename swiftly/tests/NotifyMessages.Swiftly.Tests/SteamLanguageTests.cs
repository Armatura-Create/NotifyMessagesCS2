using Xunit;

namespace NotifyMessages.Tests;

/// cl_language несёт имя языка из Steam, а конфиг ждёт код. Таблица обязана совпадать
/// с той, что зашита в SwiftlyS2, — иначе наш ответ разойдётся с фреймворком.
public class SteamLanguageTests
{
    [Theory]
    [InlineData("russian", "ru")]
    [InlineData("english", "en")]
    [InlineData("ukrainian", "uk")]
    [InlineData("polish", "pl")]
    [InlineData("german", "de")]
    [InlineData("koreana", "ko")]
    [InlineData("Russian", "ru")]
    public void SteamName_MapsToCode(string clLanguage, string expected)
        => Assert.Equal(expected, SteamLanguage.ToCode(clLanguage));

    [Theory]
    [InlineData("brazilian", "pt")]
    [InlineData("schinese", "zh")]
    [InlineData("latam", "es")]
    public void RegionalVariant_CollapsesToPrimaryCode_LikeCssharp(string clLanguage, string expected)
        => Assert.Equal(expected, SteamLanguage.ToCode(clLanguage));

    [Theory]
    [InlineData("ru", "ru")]
    [InlineData("pt-BR", "pt")]
    [InlineData("EN", "en")]
    public void ReadyMadeCode_PassesThrough(string value, string expected)
        => Assert.Equal(expected, SteamLanguage.ToCode(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("klingon")]
    [InlineData("12")]
    public void UnknownOrEmpty_IsNull(string? value)
        => Assert.Null(SteamLanguage.ToCode(value));

    [Fact]
    public void ResolvedCode_FindsTheConfigBlock()
    {
        // Сквозная проверка: "russian" -> "ru" -> блок "RU" из дефолтного Messages.json
        var index = LanguageIndex.Build(ConfigService.BuildDefaultConfig());
        Assert.Equal("RU", index.Resolve(SteamLanguage.ToCode("russian"), null, "US"));
        Assert.Equal("UA", index.Resolve(SteamLanguage.ToCode("ukrainian"), null, "US"));
    }
}
