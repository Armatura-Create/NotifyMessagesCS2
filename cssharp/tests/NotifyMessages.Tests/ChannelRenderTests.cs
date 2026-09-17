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

/// Движок CS2 не применяет цвет, стоящий в самом начале сообщения.
public class ChatColorPrefixTests
{
    private static readonly string Default = CounterStrikeSharp.API.Modules.Utils.ChatColors.Default.ToString();
    private static readonly string LightBlue = CounterStrikeSharp.API.Modules.Utils.ChatColors.LightBlue.ToString();

    [Fact]
    public void MessageStartingWithColor_GetsPrefixAndSpace()
    {
        // Регрессия: раньше здесь стоял ранний выход и такой шаблон выводился белым
        var result = TextFormatter.EnsureChatColorPrefix(LightBlue + "Server");

        Assert.Equal(Default + " " + LightBlue + "Server", result);
    }

    [Fact]
    public void LeadingSpaceInTemplate_IsNotDoubled()
    {
        var result = TextFormatter.EnsureChatColorPrefix(" " + LightBlue + "Server");

        Assert.Equal(Default + " " + LightBlue + "Server", result);
    }

    [Fact]
    public void SecondCall_ChangesNothing()
    {
        var once = TextFormatter.EnsureChatColorPrefix(LightBlue + "Server");

        Assert.Equal(once, TextFormatter.EnsureChatColorPrefix(once));
    }

    [Fact]
    public void ColorInTheMiddle_IsAlsoProtected()
    {
        // Первый код всё равно окажется первым в строке — значит и его надо прикрыть
        var result = TextFormatter.EnsureChatColorPrefix("Привет " + LightBlue + "мир");

        Assert.StartsWith(Default + " ", result, System.StringComparison.Ordinal);
        Assert.Contains(LightBlue + "мир", result, System.StringComparison.Ordinal);
    }

    [Fact]
    public void PlainText_IsLeftAlone()
    {
        // Обычному сообщению ведущий пробел ни к чему
        Assert.Equal("Просто текст", TextFormatter.EnsureChatColorPrefix("Просто текст"));
    }
}

/// Сквозная проверка: шаблон из конфига -> ProcessMessage -> строка, уходящая в PrintToChat.
/// Нативов здесь нет: ReplaceMessageTags трогает движок только если в строке есть {MAP},
/// {PLAYERS} и подобные, а их в этих шаблонах нет.
public class ColorPipelineTests
{
    private static MessageProcessor Processor() => new(
        ConfigService.BuildDefaultConfig(),
        _ => "RU");

    private static bool HasControlCodes(string text)
    {
        foreach (var c in text)
            if (c >= '\x01' && c <= '\x10') return true;
        return false;
    }

    [Fact]
    public void DefaultPrefix_ReachesChatColored()
    {
        // {prefix} в дефолтном конфиге начинается с {LIGHTBLUE} — именно этот случай выводился белым
        var processed = Processor().ProcessMessage("{prefix}Текст", 0, MessageType.Chat);
        var forChat = TextFormatter.EnsureChatColorPrefix(processed);

        Assert.True(HasControlCodes(processed));
        Assert.DoesNotContain("{", forChat, System.StringComparison.Ordinal);
        Assert.StartsWith(
            CounterStrikeSharp.API.Modules.Utils.ChatColors.Default + " " +
            CounterStrikeSharp.API.Modules.Utils.ChatColors.LightBlue,
            forChat, System.StringComparison.Ordinal);
    }

    [Fact]
    public void EveryKnownColorTag_IsReplacedInEveryChannel()
    {
        var processor = Processor();

        foreach (var tag in TextFormatter.KnownColorTags)
        {
            foreach (var channel in new[]
                     {
                         MessageType.Chat, MessageType.Center, MessageType.CenterHtml,
                         MessageType.Console, MessageType.Alert
                     })
            {
                var result = processor.ProcessMessage(tag + "X", 0, channel);

                // Ни один известный тег не имеет права доехать до игрока фигурными скобками
                Assert.DoesNotContain(tag, result, System.StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void UnknownColorTag_StaysVisibleAndIsReportedByCheck()
    {
        // Опечатка вроде {LIGHTPBLUE} остаётся текстом — и это ловит css_nm_check
        const string typo = "{LIGHTPBLUE}";
        var config = ConfigService.BuildDefaultConfig();

        Assert.Contains(typo, new MessageProcessor(config, _ => "RU")
            .ProcessMessage(typo + "X", 0, MessageType.Chat), System.StringComparison.Ordinal);

        var issues = TemplateDiagnostics.Analyze(typo, config, "test");
        Assert.Contains(issues, i => i.Severity == TemplateSeverity.Error && i.Tag == typo);
    }
}
