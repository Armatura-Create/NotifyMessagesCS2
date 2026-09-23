// Язык игрока: клиент -> страна -> DefaultLang.
//
// Порт Utils/LanguageResolver.cs (LanguageIndex) и Utils/SteamLanguage.cs.
// Язык интерфейса знает сам клиент (userinfo-квар cl_language), а IP — это
// география, а не язык: GeoIP остаётся фолбэком и источником {COUNTRY}/{CITY}.
#pragma once

#include "core/config.h"

#include <string>

namespace nm {

// Индекс языков конфига: какие блоки есть в Messages.json и какие коды на них
// отображаются через Settings.LanguageAliases. Кеширует Config, поэтому обязан
// пересобираться при mm_reload_advert.
class LanguageIndex {
public:
    static LanguageIndex Build(const Config& config);

    // Имя блока ровно как в Messages.json ("DE", а не "de"): иначе кеш рассылки
    // по языку заведёт два ключа на один язык. Пусто — не совпало ничего,
    // и defaultLang тоже пуст.
    std::string Resolve(const std::string& clientLanguage, const std::string& countryIso,
                        const std::string& defaultLang) const;

private:
    std::string Match(const std::string& code) const;

    CiMap<std::string> _available;  // блок -> написание из конфига
    CiMap<std::string> _aliases;    // код -> блок
};

namespace steam_language {

// Значение cl_language ("russian", "brazilian", "schinese") -> двухбуквенный код
// ("ru", "pt", "zh"), как это делает SourceMod и таблица l_mLanguages SwiftlyS2.
// Уже готовый код ("ru", "pt-BR") отдаётся основной частью. Пусто — не распознано.
std::string ToCode(const std::string& clLanguage);

}  // namespace steam_language
}  // namespace nm
