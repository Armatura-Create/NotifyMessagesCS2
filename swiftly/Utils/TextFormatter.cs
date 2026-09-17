using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NotifyMessages;

/// Универсальные утилиты для форматирования текста и замены цветовых тегов
public static class TextFormatter
{
    // Диапазон управляющих кодов чата CS2, используемый плагинами (\x01..\x10)
    private static readonly Regex ColorCodesRegex = new Regex("[\x01-\x10]", RegexOptions.Compiled);

    // Широкий пробел (Hangul filler) для выравнивания — тег {SPACE}
    private const string SpaceFiller = "\u3164\u3164\u3164";

    // Соответствие тегов управляющим кодам.
    //
    // Байты выписаны явно и СВЕРЕНЫ с таблицей CounterStrikeSharp (снято рефлексией
    // с пакета 1.0.369): это коды самого движка CS2, а не изобретение фреймворка,
    // и один и тот же Messages.json обязан одинаково работать под обеими целями.
    // В cssharp/ тот же список берётся из фреймворка напрямую; при расхождении
    // верна cssharp-версия — здесь просто её копия.
    private static readonly IReadOnlyDictionary<string, string> ColorTagMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{DEFAULT}"] = "\u0001",
            ["{WHITE}"] = "\u0001",
            ["{DARKRED}"] = "\u0002",
            ["{LIGHTYELLOW}"] = "\u0009",
            ["{LIGHTBLUE}"] = "\u000B",
            ["{OLIVE}"] = "\u0005",
            ["{LIME}"] = "\u0006",
            ["{GREEN}"] = "\u0004",
            ["{RED}"] = "\u0007",
            ["{LIGHTPURPLE}"] = "\u0003",
            ["{PURPLE}"] = "\u000E",
            ["{GREY}"] = "\u0008",
            ["{GRAY}"] = "\u0008",
            ["{YELLOW}"] = "\u0009",
            ["{GOLD}"] = "\u0010",
            ["{SILVER}"] = "\u000A",
            ["{BLUE}"] = "\u000B",
            ["{DARKBLUE}"] = "\u000C",
            ["{BLUEGREY}"] = "\u000A",
            ["{MAGENTA}"] = "\u000E",
            ["{LIGHTRED}"] = "\u000F",
            ["{ORANGE}"] = "\u0010"
        };

    // Порядок замены — от длинных тегов к коротким, чтобы короткий тег не съел префикс
    // длинного. Сортируется ОДИН раз: раньше LINQ-сортировка выполнялась на каждое сообщение.
    private static readonly KeyValuePair<string, string>[] SortedTags =
        ColorTagMap.OrderByDescending(kv => kv.Key.Length).ToArray();

    /// Теги, которые умеет подставить ReplaceColorTags. Диагностика шаблонов берёт список
    /// отсюда, а не заводит свой — иначе он неизбежно разъедется с реализацией.
    internal static IReadOnlySet<string> KnownColorTags { get; } =
        new HashSet<string>(ColorTagMap.Keys, StringComparer.OrdinalIgnoreCase)
        {
            "{SPACE}", "{BIG}", "{MEDIUM}", "{SMALL}"
        };

    /// Заменяет цветовые теги на управляющие коды движка CS2
    public static string ReplaceColorTags(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Быстрая проверка — если нет фигурных скобок, тегов заведомо нет
        if (input.IndexOf('{') < 0) return input;

        var result = input.Replace("{SPACE}", SpaceFiller);

        foreach (var kv in SortedTags)
        {
            if (result.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                result = Replace(result, kv.Key, kv.Value, StringComparison.OrdinalIgnoreCase);
        }

        // Размеры текста умеет только HTML-панель. В чате их вырезаем молча:
        // один шаблон может ходить в оба канала, и тег не должен доезжать текстом.
        return RemoveSizeTags(result);
    }

    // Приблизительные hex-эквиваленты цветов чата — ТОЛЬКО для HTML-центра.
    // Отдельная таблица, а не замена ColorTagMap: чат по-прежнему обязан брать коды
    // из ChatColors (своя таблица однажды уже перекрасила половину тегов).
    private static readonly IReadOnlyDictionary<string, string> HtmlColorMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{DEFAULT}"] = "#FFFFFF",
            ["{WHITE}"] = "#FFFFFF",
            ["{DARKRED}"] = "#8B0000",
            ["{LIGHTYELLOW}"] = "#FFFF99",
            ["{LIGHTBLUE}"] = "#99CCFF",
            ["{OLIVE}"] = "#9EC34F",
            ["{LIME}"] = "#00FF00",
            ["{GREEN}"] = "#3EFF3E",
            ["{RED}"] = "#FF4040",
            ["{LIGHTPURPLE}"] = "#FF99FF",
            ["{PURPLE}"] = "#8B008B",
            ["{GREY}"] = "#CCCCCC",
            ["{GRAY}"] = "#CCCCCC",
            ["{YELLOW}"] = "#FFFF00",
            ["{GOLD}"] = "#FFD700",
            ["{SILVER}"] = "#C0C0C0",
            ["{BLUE}"] = "#6699FF",
            ["{DARKBLUE}"] = "#00008B",
            ["{BLUEGREY}"] = "#6A5ACD",
            ["{MAGENTA}"] = "#FF00FF",
            ["{LIGHTRED}"] = "#FF6666",
            ["{ORANGE}"] = "#FFA500"
        };

    // Размеры HTML-панели: классы движка. Незнакомый класс панель просто игнорирует,
    // поэтому худший случай — обычный размер, а не поломанная разметка.
    private static readonly IReadOnlyDictionary<string, string> HtmlSizeMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{BIG}"] = "<font class='fontSize-l'>",
            ["{MEDIUM}"] = "<font class='fontSize-m'>",
            ["{SMALL}"] = "<font class='fontSize-sm'>"
        };

    private static readonly KeyValuePair<string, string>[] SortedHtmlTags = HtmlColorMap
        .Select(kv => new KeyValuePair<string, string>(kv.Key, $"<font color='{kv.Value}'>"))
        .Concat(HtmlSizeMap)
        .OrderByDescending(kv => kv.Key.Length)
        .ToArray();

    // Те же теги, но на выброс: канал не умеет ни цвет, ни размер.
    private static readonly string[] SortedPlainTags = HtmlColorMap.Keys
        .Concat(HtmlSizeMap.Keys)
        .OrderByDescending(k => k.Length)
        .ToArray();

    private static string RemoveSizeTags(string text)
    {
        if (text.IndexOf('{') < 0) return text;

        foreach (var tag in HtmlSizeMap.Keys)
        {
            if (text.Contains(tag, StringComparison.OrdinalIgnoreCase))
                text = Replace(text, tag, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    /// Рендер для HTML-центра: цвет тегом <font>, перенос строки — <br>.
    /// Управляющие коды чата этот канал не понимает вовсе, поэтому здесь их быть не должно.
    ///
    /// Каждый тег открывает <font>, и все они закрываются в конце строки: панель CS2
    /// терпима к незакрытой разметке, но следующее сообщение не должно наследовать
    /// цвет и размер предыдущего.
    public static string ToCenterHtml(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var result = input
            .Replace("\n", "<br>", StringComparison.Ordinal)
            .Replace("\u2029", "<br>", StringComparison.Ordinal)
            .Replace("{SPACE}", "&nbsp;&nbsp;&nbsp;", StringComparison.OrdinalIgnoreCase);

        var opened = 0;
        foreach (var kv in SortedHtmlTags)
        {
            if (!result.Contains(kv.Key, StringComparison.OrdinalIgnoreCase)) continue;

            opened += CountOccurrences(result, kv.Key);
            result = Replace(result, kv.Key, kv.Value, StringComparison.OrdinalIgnoreCase);
        }

        if (opened == 0) return result;

        var builder = new StringBuilder(result, result.Length + opened * 7);
        for (var i = 0; i < opened; i++) builder.Append("</font>");
        return builder.ToString();
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.OrdinalIgnoreCase);
        }

        return count;
    }

    /// Убирает цветовые теги, не подставляя ничего: для plain-каналов (центр, консоль, alert).
    /// Раньше туда уходили управляющие байты чата, которые эти каналы не рендерят.
    public static string RemoveColorTags(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        if (input.IndexOf('{') < 0) return input;

        var result = input.Replace("{SPACE}", SpaceFiller, StringComparison.OrdinalIgnoreCase);

        foreach (var tag in SortedPlainTags)
        {
            if (result.Contains(tag, StringComparison.OrdinalIgnoreCase))
                result = Replace(result, tag, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    /// Экранирование значений, попадающих в HTML-панель. Ник игрока — недоверенные данные:
    /// без этого «<img src=x>» в нике ломает панель всем зрителям.
    public static string EscapeHtml(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    /// Удаляет управляющие цветовые коды из строки (для логов и консоли)
    public static string StripColorCodes(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return ColorCodesRegex.Replace(input, string.Empty);
    }

    /// Приводит строку для чата к виду "\x01 " + текст.
    ///
    /// Движок CS2 не применяет цвет, стоящий в самом начале сообщения: первый цветовой код
    /// съедается, и текст выходит белым. Поэтому CounterStrikeSharp в собственном ChatMenu
    /// пишет ровно так — PrintToChat($" {color} {text}"), с пробелом перед первым цветом.
    ///
    /// Здесь ставится и код цвета по умолчанию, и пробел. Обе части нужны: код закрывает
    /// случай «съедается первый код», пробел — случай «перед первым кодом нужен обычный
    /// символ». Какая из двух моделей поведения движка верна, снаружи не различить,
    /// а сочетание работает в любой из них.
    ///
    /// Регрессия, ради которой это переписано: здесь стоял ранний выход
    /// `if (startsWithCode) return input;` — то есть починка отключалась ровно в том
    /// единственном случае, когда она нужна. Шаблон вида "{LIGHTBLUE}Текст" выводился белым,
    /// а тот же шаблон с пробелом в начале — цветным.
    ///
    /// Функция идемпотентна: повторный вызов не плодит ни коды, ни пробелы.
    public static string EnsureChatColorPrefix(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Без цветов чинить нечего — не трогаем, чтобы не приписывать пробел обычному тексту
        if (!ColorCodesRegex.IsMatch(input)) return input;

        var body = input;

        // Снимаем то, что могли поставить сами или что уже есть в шаблоне,
        // чтобы результат не зависел от того, сколько раз сюда зашли
        if (body[0] == '\u0001') body = body[1..];
        if (body.Length > 0 && body[0] == ' ') body = body[1..];

        return "\u0001 " + body;
    }

    /// Replace с игнором регистра. internal: подстановка контекстных значений
    /// ({PLAYERNAME}, {COUNTRY}, ...) обязана работать вне зависимости от того, каким регистром
    /// админ написал тег в конфиге — дефолтный Messages.json пишет их строчными.
    internal static string ReplaceIgnoreCase(string text, string search, string replacement)
        => Replace(text, search, replacement, StringComparison.OrdinalIgnoreCase);

    // Вспомогательный Replace с игнором регистра
    private static string Replace(this string text, string search, string replacement, StringComparison comparison)
    {
        int index = text.IndexOf(search, comparison);
        if (index < 0) return text;

        var result = new StringBuilder(text.Length);
        int lastIndex = 0;
        while (index >= 0)
        {
            result.Append(text, lastIndex, index - lastIndex);
            result.Append(replacement);
            lastIndex = index + search.Length;
            index = text.IndexOf(search, lastIndex, comparison);
        }
        result.Append(text, lastIndex, text.Length - lastIndex);
        return result.ToString();
    }
}
