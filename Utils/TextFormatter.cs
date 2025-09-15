using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NotifyMessages;

/// Универсальные утилиты для форматирования текста и замены цветовых тегов
public static class TextFormatter
{
    // Диапазон управляющих кодов чата CS2, используемый плагинами (\x01..\x10)
    private static readonly Regex ColorCodesRegex = new Regex("[\x01-\x10]", RegexOptions.Compiled);

    // Соответствие тегов цветов управляющим кодам
    private static readonly IReadOnlyDictionary<string, string> ColorTagMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Базовые
        ["{DEFAULT}"] = "\x01",
        ["{WHITE}"] = "\x01",
        ["{DARKRED}"] = "\x02",
        ["{LIGHTYELLOW}"] = "\x03",
        ["{LIGHTBLUE}"] = "\x04",
        ["{OLIVE}"] = "\x05",
        ["{LIME}"] = "\x06",
        ["{GREEN}"] = "\x06", // часто используемый синоним
        ["{RED}"] = "\x07",
        ["{LIGHTPURPLE}"] = "\x08",
        ["{PURPLE}"] = "\x09",
        ["{GREY}"] = "\x0A",
        ["{GRAY}"] = "\x0A",
        ["{YELLOW}"] = "\x0B",
        ["{GOLD}"] = "\x0B", // синоним жёлтого
        ["{SILVER}"] = "\x0D",
        ["{BLUE}"] = "\x0E",
        ["{DARKBLUE}"] = "\x0F",
        ["{BLUEGREY}"] = "\x10",

        // Дополнительные (мапим к ближайшему доступному)
        ["{MAGENTA}"] = "\x08",     // близко к фиолетовому
        ["{LIGHTRED}"] = "\x07",    // как красный
        ["{ORANGE}"] = "\x0B"       // как жёлтый
    };

    /// Заменяет цветовые теги на управляющие коды движка CS2
    public static string ReplaceColorTags(this string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Быстрая проверка — если нет фигурных скобок, вероятно, тегов нет
        if (input.IndexOf('{') < 0) return input;

        string result = input;

        // Поддержка спец-тега пробела
        result = result.Replace("{SPACE}", "\u3164\u3164\u3164"); // несколько широких пробелов

        foreach (var kv in ColorTagMap)
        {
            if (result.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                result = result.Replace(kv.Key, kv.Value, StringComparison.OrdinalIgnoreCase);
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
        return ColorCodesRegex.IsMatch(input) ? "\x01" + input : input;
    }

    // Вспомогательный Replace с игнором регистра
    private static string Replace(this string text, string search, string replacement, StringComparison comparison)
    {
        int index = text.IndexOf(search, comparison);
        if (index < 0) return text;

        var result = new System.Text.StringBuilder(text.Length);
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