using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API.Modules.Utils;

namespace NotifyMessages;

/// Универсальные утилиты для форматирования текста и замены цветовых тегов
public static class TextFormatter
{
    // Диапазон управляющих кодов чата CS2, используемый плагинами (\x01..\x10)
    private static readonly Regex ColorCodesRegex = new Regex("[\x01-\x10]", RegexOptions.Compiled);

    // Широкий пробел (Hangul filler) для выравнивания — тег {SPACE}
    private const string SpaceFiller = "\u3164\u3164\u3164";

    // Соответствие тегов управляющим кодам.
    // ИСТОЧНИК ИСТИНЫ — CounterStrikeSharp.API.Modules.Utils.ChatColors: раньше здесь была
    // своя таблица из CS:GO/SourceMod, из-за чего половина тегов давала не тот цвет
    // ({BLUE} рисовался пурпурным, {YELLOW} синим, {LIGHTBLUE} зелёным и т.д.).
    private static readonly IReadOnlyDictionary<string, string> ColorTagMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{DEFAULT}"] = ChatColors.Default.ToString(),
            ["{WHITE}"] = ChatColors.White.ToString(),
            ["{DARKRED}"] = ChatColors.DarkRed.ToString(),
            ["{LIGHTYELLOW}"] = ChatColors.LightYellow.ToString(),
            ["{LIGHTBLUE}"] = ChatColors.LightBlue.ToString(),
            ["{OLIVE}"] = ChatColors.Olive.ToString(),
            ["{LIME}"] = ChatColors.Lime.ToString(),
            ["{GREEN}"] = ChatColors.Green.ToString(),
            ["{RED}"] = ChatColors.Red.ToString(),
            ["{LIGHTPURPLE}"] = ChatColors.LightPurple.ToString(),
            ["{PURPLE}"] = ChatColors.Purple.ToString(),
            ["{GREY}"] = ChatColors.Grey.ToString(),
            ["{GRAY}"] = ChatColors.Grey.ToString(),
            ["{YELLOW}"] = ChatColors.Yellow.ToString(),
            ["{GOLD}"] = ChatColors.Gold.ToString(),
            ["{SILVER}"] = ChatColors.Silver.ToString(),
            ["{BLUE}"] = ChatColors.Blue.ToString(),
            ["{DARKBLUE}"] = ChatColors.DarkBlue.ToString(),
            ["{BLUEGREY}"] = ChatColors.BlueGrey.ToString(),
            ["{MAGENTA}"] = ChatColors.Magenta.ToString(),
            ["{LIGHTRED}"] = ChatColors.LightRed.ToString(),
            ["{ORANGE}"] = ChatColors.Orange.ToString()
        };

    // Порядок замены — от длинных тегов к коротким, чтобы короткий тег не съел префикс
    // длинного. Сортируется ОДИН раз: раньше LINQ-сортировка выполнялась на каждое сообщение.
    private static readonly KeyValuePair<string, string>[] SortedTags =
        ColorTagMap.OrderByDescending(kv => kv.Key.Length).ToArray();

    /// Заменяет цветовые теги на управляющие коды движка CS2
    public static string ReplaceColorTags(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Быстрая проверка — если нет фигурных скобок, тегов заведомо нет
        if (input.IndexOf('{') < 0) return input;

        var result = input.Replace("{SPACE}", SpaceFiller);

        foreach (var kv in SortedTags)
        {
            if (result.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                result = Replace(result, kv.Key, kv.Value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    /// Удаляет управляющие цветовые коды из строки (для логов и консоли)
    public static string StripColorCodes(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return ColorCodesRegex.Replace(input, string.Empty);
    }

    /// Гарантирует, что строка для чата начинается с дефолтного цветового кода
    public static string EnsureChatColorPrefix(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        char first = input[0];
        bool startsWithCode = first >= '\x01' && first <= '\x10';
        if (startsWithCode) return input;
        // Префиксуем только если в строке уже есть цветовые коды
        return ColorCodesRegex.IsMatch(input) ? ChatColors.Default + input : input;
    }

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
