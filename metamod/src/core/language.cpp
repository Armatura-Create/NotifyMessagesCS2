#include "core/language.h"

#include <cctype>
#include <cstring>

namespace nm {
namespace {

bool IsBlank(const std::string& value) {
    for (const char c : value) {
        if (!std::isspace(static_cast<unsigned char>(c))) return false;
    }
    return true;
}

}  // namespace

LanguageIndex LanguageIndex::Build(const Config& config) {
    LanguageIndex index;

    for (const auto& key : config.languageMessages) {
        for (const auto& lang : key.second) index._available.emplace(lang.first, lang.first);
    }

    // Конфиг задаёт «блок -> коды», а искать надо наоборот: код -> блок
    for (const auto& alias : config.languageAliases) {
        for (const std::string& code : alias.second) {
            if (!IsBlank(code)) index._aliases[code] = alias.first;
        }
    }
    return index;
}

std::string LanguageIndex::Resolve(const std::string& clientLanguage, const std::string& countryIso,
                                   const std::string& defaultLang) const {
    std::string block = Match(clientLanguage);
    if (block.empty()) block = Match(countryIso);
    return block.empty() ? defaultLang : block;
}

std::string LanguageIndex::Match(const std::string& code) const {
    if (IsBlank(code)) return std::string();

    const auto direct = _available.find(code);
    if (direct != _available.end()) return direct->second;

    const auto alias = _aliases.find(code);
    if (alias == _aliases.end()) return std::string();

    const auto aliased = _available.find(alias->second);
    return aliased != _available.end() ? aliased->second : std::string();
}

namespace steam_language {
namespace {

struct Entry {
    const char* steam;
    const char* code;
};

// Та же таблица, что l_mLanguages в SwiftlyS2 (src/server/translations/translations.cpp)
constexpr Entry kLanguages[] = {
    {"arabic", "ar"},     {"bulgarian", "bg"},  {"schinese", "zh-CN"}, {"tchinese", "zh-TW"},
    {"czech", "cs"},      {"danish", "da"},     {"dutch", "nl"},       {"english", "en"},
    {"finnish", "fi"},    {"french", "fr"},     {"german", "de"},      {"greek", "el"},
    {"hungarian", "hu"},  {"indonesian", "id"}, {"italian", "it"},     {"japanese", "ja"},
    {"koreana", "ko"},    {"norwegian", "no"},  {"polish", "pl"},      {"portuguese", "pt"},
    {"brazilian", "pt-BR"}, {"romanian", "ro"}, {"russian", "ru"},     {"spanish", "es"},
    {"latam", "es-419"},  {"swedish", "sv"},    {"thai", "th"},        {"turkish", "tr"},
    {"ukrainian", "uk"},  {"vietnamese", "vn"},
};

bool EqualsIgnoreCase(const std::string& a, const char* b) {
    if (a.size() != std::strlen(b)) return false;
    for (size_t i = 0; i < a.size(); ++i) {
        if (std::tolower(static_cast<unsigned char>(a[i])) != std::tolower(static_cast<unsigned char>(b[i]))) {
            return false;
        }
    }
    return true;
}

std::string Primary(const std::string& code) {
    const size_t dash = code.find('-');
    return dash != std::string::npos && dash > 0 ? code.substr(0, dash) : code;
}

}  // namespace

std::string ToCode(const std::string& clLanguage) {
    size_t begin = 0;
    size_t end = clLanguage.size();
    while (begin < end && std::isspace(static_cast<unsigned char>(clLanguage[begin]))) ++begin;
    while (end > begin && std::isspace(static_cast<unsigned char>(clLanguage[end - 1]))) --end;
    const std::string value = clLanguage.substr(begin, end - begin);
    if (value.empty()) return std::string();

    for (const Entry& entry : kLanguages) {
        if (EqualsIgnoreCase(value, entry.steam)) return Primary(entry.code);
    }

    // Не имя из Steam, но похоже на код: "ru", "en", "pt-BR"
    std::string primary = Primary(value);
    if (primary.size() != 2 && primary.size() != 3) return std::string();
    for (char& c : primary) {
        if (!std::isalpha(static_cast<unsigned char>(c))) return std::string();
        c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
    }
    return primary;
}

}  // namespace steam_language
}  // namespace nm
