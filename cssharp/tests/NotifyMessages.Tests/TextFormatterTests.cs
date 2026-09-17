using CounterStrikeSharp.API.Modules.Utils;
using Xunit;

namespace NotifyMessages.Tests;

public class TextFormatterTests
{
    [Fact]
    public void ReplaceColorTags_UsesCounterStrikeSharpCodes()
    {
        // Регрессия: до 2.1.0 таблица была своя и {BLUE} рисовался пурпурным
        Assert.Equal(ChatColors.Blue.ToString(), "{BLUE}".ReplaceColorTags());
        Assert.Equal(ChatColors.Yellow.ToString(), "{YELLOW}".ReplaceColorTags());
        Assert.Equal(ChatColors.Green.ToString(), "{GREEN}".ReplaceColorTags());
        Assert.Equal(ChatColors.Grey.ToString(), "{GREY}".ReplaceColorTags());
    }

    [Fact]
    public void ReplaceColorTags_IsCaseInsensitive()
    {
        Assert.Equal(ChatColors.Red.ToString(), "{red}".ReplaceColorTags());
    }

    [Fact]
    public void ReplaceColorTags_LongTagIsNotEatenByShorterOne()
    {
        // {LIGHTBLUE} не должен схлопнуться в {BLUE}: замена идёт от длинных к коротким
        Assert.Equal(ChatColors.LightBlue.ToString(), "{LIGHTBLUE}".ReplaceColorTags());
        Assert.NotEqual("LIGHT" + ChatColors.Blue, "{LIGHTBLUE}".ReplaceColorTags());
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
        Assert.StartsWith(ChatColors.Default.ToString(), TextFormatter.EnsureChatColorPrefix(colored));
    }
}
