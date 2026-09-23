// Порт TemplateDiagnosticsTests.cs.
#include "core/config.h"
#include "core/template_diagnostics.h"

#include "doctest.h"

using namespace nm;

namespace {

Config Sample() {
    Config config;
    config.defaultLang = "RU";
    config.languageMessages["prefix"]["RU"] = "{LIGHTBLUE}Server ➡{DEFAULT} ";
    config.languageMessages["prefix"]["US"] = "{LIGHTBLUE}Server ➡{DEFAULT} ";
    config.languageMessages["hello"]["RU"] = "Привет";
    config.languageMessages["hello"]["US"] = "Hello";
    config.languageMessages["broken"]["RU"] = "Текст с {opechatka}";
    config.languageMessages["broken"]["US"] = "Text with {opechatka}";
    return config;
}

}  // namespace

TEST_CASE("Неизвестный тег — ошибка") {
    const auto issues = AnalyzeTemplate("{prefix}{reklama_9}", Sample(), "Ads.json");
    REQUIRE(issues.size() == 1);
    CHECK(issues[0].severity == TemplateSeverity::Error);
    CHECK(issues[0].tag == "{reklama_9}");
}

TEST_CASE("Цветовые и системные теги не помечаются, регистр не важен") {
    CHECK(AnalyzeTemplate("{RED}{SPACE}{MAP} {PLAYERS}/{MAXPLAYERS} {SERVERNAME}{DEFAULT}", Sample(), "test").empty());
    CHECK(AnalyzeTemplate("{red}{Default}", Sample(), "test").empty());
}

TEST_CASE("Контекстный тег не на своём месте — предупреждение") {
    // {SECONDS} подставляет только mm_restart_notify — в рекламе он останется текстом
    const auto issues = AnalyzeTemplate("{SECONDS}", Sample(), "Ads.json");
    REQUIRE(issues.size() == 1);
    CHECK(issues[0].severity == TemplateSeverity::Warning);
    CHECK(issues[0].text.find("RestartNotify") != std::string::npos);

    CHECK(AnalyzeTemplate("{SECONDS}", Sample(), "RestartNotify", {"{SECONDS}"}).empty());
}

TEST_CASE("Опечатка внутри перевода находится") {
    const auto issues = AnalyzeTemplate("{broken}", Sample(), "Settings.json");
    REQUIRE_FALSE(issues.empty());
    bool whereRu = false;
    for (const TemplateIssue& issue : issues) {
        CHECK(issue.tag == "{opechatka}");
        whereRu = whereRu || issue.where.find("{broken}[RU]") != std::string::npos;
    }
    CHECK(whereRu);
}

TEST_CASE("Циклические ключи не вешают анализ") {
    Config config;
    config.languageMessages["a"]["RU"] = "{b}";
    config.languageMessages["b"]["RU"] = "{a}";
    CHECK(AnalyzeTemplate("{a}", config, "test").empty());
}

TEST_CASE("Дыра в переводах — предупреждение") {
    Config config;
    config.defaultLang = "RU";
    config.languageMessages["full"]["RU"] = "раз";
    config.languageMessages["full"]["US"] = "one";
    config.languageMessages["partial"]["RU"] = "два";

    const auto issues = AnalyzeLanguageCoverage(config);
    REQUIRE(issues.size() == 1);
    CHECK(issues[0].severity == TemplateSeverity::Warning);
    CHECK(issues[0].tag == "{partial}");
    CHECK(issues[0].text.find("US") != std::string::npos);
}

TEST_CASE("Строка претензии") {
    const TemplateIssue issue{TemplateSeverity::Error, "Ads.json", "{x}", "плохо"};
    CHECK(issue.ToString() == "[ОШИБКА] Ads.json: плохо");
}
