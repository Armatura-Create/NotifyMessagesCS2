using System;
using System.Collections.Generic;

namespace NotifyMessages;

/// Индекс языков конфига: какие языковые блоки есть в Messages.json и какие коды
/// на них отображаются.
///
/// Зачем: язык игрока знает сам движок (cl_language), а IP — это география, а не язык.
/// Игрок из Казахстана с русским клиентом раньше получал DefaultLang, потому что
/// блока "KZ" в конфиге нет и никогда не будет — дублировать все переводы на каждую
/// страну бессмысленно. Алиасы решают это одной строкой в Settings.json.
///
/// Строится один раз на загрузку конфига: разбирать словари на каждое сообщение незачем.
internal sealed class LanguageIndex
{
    private readonly HashSet<string> _available;
    private readonly Dictionary<string, string> _aliases;

    private LanguageIndex(HashSet<string> available, Dictionary<string, string> aliases)
    {
        _available = available;
        _aliases = aliases;
    }

    public static LanguageIndex Build(Config config)
    {
        var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (config.LanguageMessages != null)
        {
            foreach (var translations in config.LanguageMessages.Values)
            {
                if (translations == null) continue;
                foreach (var lang in translations.Keys) available.Add(lang);
            }
        }

        // Конфиг задаёт «блок -> коды», а искать надо наоборот: код -> блок
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (config.LanguageAliases != null)
        {
            foreach (var (block, codes) in config.LanguageAliases)
            {
                if (codes == null) continue;
                foreach (var code in codes)
                {
                    if (!string.IsNullOrWhiteSpace(code)) aliases[code] = block;
                }
            }
        }

        return new LanguageIndex(available, aliases);
    }

    /// Порядок источников: язык клиента -> страна по GeoIP -> DefaultLang.
    /// Язык клиента точнее геолокации: он выбран самим игроком.
    public string? Resolve(string? clientLanguage, string? countryIso, string? defaultLang)
        => Match(clientLanguage) ?? Match(countryIso) ?? defaultLang;

    /// Возвращает имя блока ровно так, как оно записано в Messages.json.
    /// Движок отдаёт "de", в конфиге "DE" — вернуть надо конфигурационное написание,
    /// иначе кеш рассылки в DisplayService заведёт два ключа на один язык.
    private string? Match(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        if (_available.TryGetValue(code, out var canonical)) return canonical;

        return _aliases.TryGetValue(code, out var block) && _available.TryGetValue(block, out var aliased)
            ? aliased
            : null;
    }
}
