using System;
using System.Collections.Generic;

namespace NotifyMessages;

/// Язык интерфейса игры -> код языка, как это делает SourceMod (languages.cfg).
///
/// Клиент CS2 несёт язык интерфейса в userinfo-кваре cl_language именем из Steam:
/// "russian", "english", "brazilian", "schinese". Таблица совпадает с той, что зашита
/// в самом SwiftlyS2 (src/server/translations/translations.cpp, l_mLanguages), поэтому
/// наш ответ никогда не разойдётся с тем, что фреймворк подставил бы сам.
///
/// Чистый класс без единого типа фреймворка: проверяется тестами на любой машине.
public static class SteamLanguage
{
    private static readonly IReadOnlyDictionary<string, string> Codes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["arabic"] = "ar",
            ["bulgarian"] = "bg",
            ["schinese"] = "zh-CN",
            ["tchinese"] = "zh-TW",
            ["czech"] = "cs",
            ["danish"] = "da",
            ["dutch"] = "nl",
            ["english"] = "en",
            ["finnish"] = "fi",
            ["french"] = "fr",
            ["german"] = "de",
            ["greek"] = "el",
            ["hungarian"] = "hu",
            ["indonesian"] = "id",
            ["italian"] = "it",
            ["japanese"] = "ja",
            ["koreana"] = "ko",
            ["norwegian"] = "no",
            ["polish"] = "pl",
            ["portuguese"] = "pt",
            ["brazilian"] = "pt-BR",
            ["romanian"] = "ro",
            ["russian"] = "ru",
            ["spanish"] = "es",
            ["latam"] = "es-419",
            ["swedish"] = "sv",
            ["thai"] = "th",
            ["turkish"] = "tr",
            ["ukrainian"] = "uk",
            ["vietnamese"] = "vn"
        };

    /// Двухбуквенный код языка по значению cl_language, либо null, если значение пустое
    /// или неизвестное. Принимает и уже готовый код ("ru", "pt-BR") — тогда возвращает
    /// его основную часть, как TwoLetterISOLanguageName в cssharp/.
    public static string? ToCode(string? clLanguage)
    {
        if (string.IsNullOrWhiteSpace(clLanguage)) return null;

        var value = clLanguage.Trim();

        if (Codes.TryGetValue(value, out var code)) return Primary(code);

        // Не имя из Steam, но похоже на код: "ru", "en", "pt-BR"
        var primary = Primary(value);
        return primary.Length is 2 or 3 && IsLetters(primary) ? primary.ToLowerInvariant() : null;
    }

    private static string Primary(string code)
    {
        var dash = code.IndexOf('-', StringComparison.Ordinal);
        return dash > 0 ? code[..dash] : code;
    }

    private static bool IsLetters(string value)
    {
        foreach (var c in value)
        {
            if (!char.IsAsciiLetter(c)) return false;
        }

        return true;
    }
}
