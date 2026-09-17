using Xunit;

namespace NotifyMessages.Tests;

public class TextFormatterTests
{
    [Fact]
    public void ReplaceColorTags_UsesCounterStrikeSharpCodes()
    {
        // Регрессия: до 2.1.0 таблица была своя и {BLUE} рисовался пурпурным
        Assert.Equal("\u000B", "{BLUE}".ReplaceColorTags());
        Assert.Equal("\u0009", "{YELLOW}".ReplaceColorTags());
        Assert.Equal("\u0004", "{GREEN}".ReplaceColorTags());
        Assert.Equal("\u0008", "{GREY}".ReplaceColorTags());
    }

    [Fact]
    public void ReplaceColorTags_IsCaseInsensitive()
    {
        Assert.Equal("\u0007", "{red}".ReplaceColorTags());
    }

    [Fact]
    public void ReplaceColorTags_LongTagIsNotEatenByShorterOne()
    {
        // {LIGHTBLUE} не должен схлопнуться в {BLUE}: замена идёт от длинных к коротким
        Assert.Equal("\u000B", "{LIGHTBLUE}".ReplaceColorTags());
        Assert.NotEqual("LIGHT" + "\u000B", "{LIGHTBLUE}".ReplaceColorTags());
    }

    [Fact]
    public void ReplaceColorTags_LeavesUnknownTagsAlone()
    {
        Assert.Equal("{NOT_A_COLOR}", "{NOT_A_COLOR}".ReplaceColorTags());
    }

    [Fact]
    public void StripColorCodes_RemovesControlChars()
    {
        var colored = "{RED}hello{DEFAULT}".ReplaceColorTags();
        Assert.Equal("hello", TextFormatter.StripColorCodes(colored));
    }

    [Fact]
    public void EnsureChatColorPrefix_OnlyPrefixesWhenColorsPresent()
    {
        Assert.Equal("plain", TextFormatter.EnsureChatColorPrefix("plain"));

        var colored = "text{RED}red".ReplaceColorTags();
        Assert.StartsWith("\u0001", TextFormatter.EnsureChatColorPrefix(colored));
    }
}
