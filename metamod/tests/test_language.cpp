// Язык игрока знает клиент (cl_language); IP — это география, а не язык.
// Порт LanguageIndexTests.cs и SteamLanguageTests.cs (swiftly/).
#include "core/config.h"
#include "core/language.h"

#include "doctest.h"

using namespace nm;

namespace {

Config Sample(std::vector<std::pair<std::string, std::vector<std::string>>> aliases = {}) {
    Config config;
    config.defaultLang = "RU";
    config.languageAliases = std::move(aliases);
    config.languageMessages["prefix"]["RU"] = "рус";
    config.languageMessages["prefix"]["US"] = "eng";
    return config;
}

}  // namespace

TEST_CASE("Язык клиента важнее GeoIP") {
    const LanguageIndex index = LanguageIndex::Build(Sample({{"US", {"en"}}}));
    CHECK(index.Resolve("en", "DE", "RU") == "US");
}

TEST_CASE("Страна — когда язык клиента неизвестен") {
    CHECK(LanguageIndex::Build(Sample()).Resolve("", "US", "RU") == "US");
}

TEST_CASE("Страна без своего блока идёт через алиас") {
    // Регрессия: игрок из Казахстана получал DefaultLang, потому что блока "KZ" нет
    const LanguageIndex index = LanguageIndex::Build(Sample({{"RU", {"ru", "KZ", "BY"}}}));
    CHECK(index.Resolve("", "KZ", "US") == "RU");
}

TEST_CASE("Ничего не совпало — DefaultLang") {
    CHECK(LanguageIndex::Build(Sample()).Resolve("zz", "ZZ", "RU") == "RU");
}

TEST_CASE("Алиас на отсутствующий блок игнорируется") {
    const LanguageIndex index = LanguageIndex::Build(Sample({{"FR", {"fr"}}}));
    CHECK(index.Resolve("fr", "", "RU") == "RU");
}

TEST_CASE("Регистр кода языка не важен, возвращается написание из конфига") {
    CHECK(LanguageIndex::Build(Sample()).Resolve("ru", "", "US") == "RU");
}

TEST_CASE("Дефолтный конфиг сводит языки клиента к своим блокам") {
    const LanguageIndex index = LanguageIndex::Build(ConfigService::BuildDefaultConfig());
    CHECK(index.Resolve("en", "", "RU") == "US");
    CHECK(index.Resolve("uk", "", "RU") == "UA");
    CHECK(index.Resolve("de", "", "RU") == "DE");
    CHECK(index.Resolve("", "KZ", "US") == "RU");
}

TEST_CASE("cl_language: имя из Steam -> код") {
    CHECK(steam_language::ToCode("russian") == "ru");
    CHECK(steam_language::ToCode("english") == "en");
    CHECK(steam_language::ToCode("ukrainian") == "uk");
    CHECK(steam_language::ToCode("polish") == "pl");
    CHECK(steam_language::ToCode("german") == "de");
    CHECK(steam_language::ToCode("koreana") == "ko");
    CHECK(steam_language::ToCode("Russian") == "ru");
    // Региональные варианты сводятся к основному коду, как в cssharp/
    CHECK(steam_language::ToCode("brazilian") == "pt");
    CHECK(steam_language::ToCode("schinese") == "zh");
    CHECK(steam_language::ToCode("latam") == "es");
    // Готовый код проходит насквозь
    CHECK(steam_language::ToCode("ru") == "ru");
    CHECK(steam_language::ToCode("pt-BR") == "pt");
    CHECK(steam_language::ToCode("EN") == "en");
    // Пусто и мусор — неизвестно
    CHECK(steam_language::ToCode("").empty());
    CHECK(steam_language::ToCode("   ").empty());
    CHECK(steam_language::ToCode("klingon").empty());
    CHECK(steam_language::ToCode("12").empty());
}

TEST_CASE("cl_language доходит до блока конфига") {
    const LanguageIndex index = LanguageIndex::Build(ConfigService::BuildDefaultConfig());
    CHECK(index.Resolve(steam_language::ToCode("russian"), "", "US") == "RU");
    CHECK(index.Resolve(steam_language::ToCode("ukrainian"), "", "US") == "UA");
}
