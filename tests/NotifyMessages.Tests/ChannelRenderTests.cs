using System;
using System.Collections.Generic;
using Xunit;

namespace NotifyMessages.Tests;

/// У каналов вывода разные грамматики. Одна строка не может быть корректной во всех:
/// чат понимает управляющие байты, HTML-панель — разметку, центр и консоль — только текст.
public class ChannelRenderTests
{
    private static bool HasControlCodes(string text)
    {
        foreach (var c in text)
            if (c >= '\x01' && c <= '\x10') return true;
        return false;
    }

    [Fact]
    public void Chat_UsesControlCodes()
    {
        var result = MessageProcessor.Render("{RED}Привет", MessageType.Chat);

        Assert.True(HasControlCodes(result));
        Assert.DoesNotContain("{RED}", result, StringComparison.Ordinal);
    }

    [Fact]
    public void CenterHtml_UsesMarkupInsteadOfControlCodes()
    {
        // Регрессия: раньше в PrintToCenterHtml уходили чат-байты и U+2029,
        // то есть HTML-центр не умел ни цвета, ни переносы строк.
        var result = MessageProcessor.Render("{RED}Строка\nВторая", MessageType.CenterHtml);

        Assert.Contains("<font color='#FF4040'>", result, StringComparison.Ordinal);
        Assert.Contains("<br>", result, StringComparison.Ordinal);
        Assert.False(HasControlCodes(result));
        Assert.DoesNotContain("\u2029", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Center_IsPlainText()
    {
        var result = MessageProcessor.Render("{RED}Привет{DEFAULT}", MessageType.Center);

        Assert.Equal("Привет", result);
        Assert.False(HasControlCodes(result));
    }

    [Fact]
    public void Console_KeepsRealNewline()
    {
        // U+2029 в консоли — мусорный символ, там нужен настоящий перенос строки
        var result = MessageProcessor.Render("{GREEN}раз\nдва", MessageType.Console);

        Assert.Equal("раз\nдва", result);
    }

    [Fact]
    public void Center_ConvertsNewlineToParagraphSeparator()
    {
        Assert.Contains("\u2029", MessageProcessor.Render("раз\nдва", MessageType.Center),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Values_AreSubstitutedIgnoringCase()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{COUNTRY}"] = "Poland"
        };

        // Дефолтный Messages.json пишет этот тег строчными — регистр не должен ничего ломать
        Assert.Equal("Poland", MessageProcessor.ApplyValues("{country}", values, MessageType.Chat));
    }

    [Fact]
    public void Values_KeepColorTagsForLaterRendering()
    {
        // Регрессия: смена команды показывала литеральное "{RED}Terrorists{DEFAULT}",
        // потому что значение подставляли уже после рендера
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{TEAM}"] = "{RED}Terrorists{DEFAULT}"
        };

        var withValues = MessageProcessor.ApplyValues("перешёл в {TEAM}", values, MessageType.Chat);
        var rendered = MessageProcessor.Render(withValues, MessageType.Chat);

        Assert.DoesNotContain("{RED}", rendered, StringComparison.Ordinal);
        Assert.True(HasControlCodes(rendered));
    }

    [Fact]
    public void Values_AreHtmlEscapedForHtmlChannelOnly()
    {
        // Ник — недоверенные данные: без экранирования он ломает HTML-панель всем зрителям
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{PLAYERNAME}"] = "<img src=x>"
        };

        Assert.Equal("&lt;img src=x&gt;",
            MessageProcessor.ApplyValues("{PLAYERNAME}", values, MessageType.CenterHtml));
        Assert.Equal("<img src=x>",
            MessageProcessor.ApplyValues("{PLAYERNAME}", values, MessageType.Chat));
    }
}
