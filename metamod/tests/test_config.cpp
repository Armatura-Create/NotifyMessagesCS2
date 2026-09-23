// Кривой конфиг — самая частая проблема у пользователей: он обязан приводить
// к внятному сообщению, а не к падению плагина. Порт ConfigResilienceTests.cs
// и ConfigBehaviourTests.cs.
#include "core/config.h"
#include "core/template_diagnostics.h"
#include "helpers.h"

#include "doctest.h"

#include <fstream>
#include <sstream>
#include <sys/stat.h>

using namespace nm;
using nm_test::Contains;

namespace {

void Write(const std::string& path, const std::string& content) {
    std::ofstream(path.c_str(), std::ios::binary) << content;
}

std::string Read(const std::string& path) {
    std::ifstream stream(path.c_str(), std::ios::binary);
    std::ostringstream buffer;
    buffer << stream.rdbuf();
    return buffer.str();
}

bool Exists(const std::string& path) {
    struct stat info;
    return stat(path.c_str(), &info) == 0;
}

int CountContaining(const std::vector<std::string>& lines, const std::string& part) {
    int count = 0;
    for (const std::string& line : lines) count += Contains(line, part) ? 1 : 0;
    return count;
}

}  // namespace

TEST_CASE("Первый запуск создаёт четыре конфига, схемы и README") {
    nm_test::TempDir dir;
    nm_test::RecordingLogger logger;
    const std::string configs = dir.path + "/addons/configs/NotifyMessages";

    const Config config = ConfigService(&logger).LoadOrCreate(configs);

    for (const char* name : {"Settings.json", "Messages.json", "Ads.json", "Servers.json", "README.txt",
                             "Settings.schema.json", "Messages.schema.json", "Ads.schema.json",
                             "Servers.schema.json"}) {
        CAPTURE(name);
        CHECK(Exists(configs + "/" + name));
    }

    // Ссылка на схему — первым свойством, иначе редактор её не подхватит.
    // BOM — как у File.WriteAllText(..., Encoding.UTF8) в C#.
    const std::string settings = Read(configs + "/Settings.json");
    CHECK(settings.compare(0, 3, "\xEF\xBB\xBF") == 0);
    CHECK(Contains(settings, "\"$schema\": \"./Settings.schema.json\""));

    CHECK(config.welcomeMessage.has_value());
    CHECK(logger.errors.empty());
}

TEST_CASE("Дефолтный конфиг — тот, что пишет C#-цель") {
    const Config config = ConfigService::BuildDefaultConfig();

    CHECK(config.defaultLang == "RU");
    CHECK_FALSE(config.debug);
    REQUIRE(config.welcomeMessage.has_value());
    CHECK(config.welcomeMessage->messageType == MessageType::Chat);
    CHECK(config.welcomeMessage->displayDelay == doctest::Approx(5.0));
    REQUIRE(config.restartNotify.has_value());
    CHECK(config.restartNotify->thresholds.size() == 9);
    CHECK(config.ads.size() == 6);
    CHECK(config.ads[0].interval == doctest::Approx(120.0));
    REQUIRE(config.ads[0].messages.size() == 2);
    CHECK(config.ads[0].messages[1][0].first == "CenterHtml");
    REQUIRE(config.servers.has_value());
    CHECK_FALSE(config.servers->enabled);
    CHECK(config.servers->list.size() == 2);
    CHECK(config.servers->list[0].maxPlayersFallback == 32);
    CHECK(config.languageMessages.count("prefix") == 1);
    CHECK(config.languageMessages.at("PREFIX").at("ru") == "{LIGHTBLUE}Server ➡{DEFAULT} ");
    CHECK(config.joinMessages.count("us") == 1);
    CHECK(config.mapsName.size() == 8);
    CHECK(config.languageAliases.size() == 5);

    // Сырые байты: в C# это "Хочешь крутые скины? ...\n" с настоящим \n внутри строки
    CHECK(Contains(config.languageMessages.at("reklama_1").at("RU"), "\n"));
}

TEST_CASE("В дефолтах нет ни одной ошибки шаблона") {
    // Регрессия: первый запуск не имеет права показать игроку тег в фигурных скобках
    for (const TemplateIssue& issue : CollectIssues(ConfigService::BuildDefaultConfig())) {
        CAPTURE(issue.ToString());
        CHECK(issue.severity != TemplateSeverity::Error);
    }
}

TEST_CASE("$schema игнорируется при чтении") {
    nm_test::TempDir dir;
    Write(dir.path + "/Settings.json", "{ \"$schema\": \"./Settings.schema.json\", \"DefaultLang\": \"PL\" }");

    nm_test::RecordingLogger logger;
    const Config config = ConfigService(&logger).LoadOrCreate(dir.path);

    CHECK(config.defaultLang == "PL");
    CHECK(logger.errors.empty());
}

TEST_CASE("MessageType читается и строкой, и числом") {
    nm_test::TempDir dir;
    Write(dir.path + "/Settings.json",
          "{ \"WelcomeMessage\": { \"MessageType\": \"CenterHtml\", \"Message\": \"hi\" },"
          "  \"RestartNotify\": { \"MessageType\": 4, \"DefaultMessage\": \"x\" } }");

    nm_test::RecordingLogger logger;
    const Config config = ConfigService(&logger).LoadOrCreate(dir.path);

    REQUIRE(config.welcomeMessage.has_value());
    CHECK(config.welcomeMessage->messageType == MessageType::CenterHtml);
    REQUIRE(config.restartNotify.has_value());
    CHECK(config.restartNotify->messageType == MessageType::Alert);
}

TEST_CASE("Битый JSON не роняет загрузку и называет файл, строку и позицию") {
    nm_test::TempDir dir;
    // Пропущена запятая между полями — ошибка на третьей строке
    Write(dir.path + "/Settings.json", "{\n  \"Debug\": true\n  \"DefaultLang\": \"RU\"\n}");
    Write(dir.path + "/Messages.json", "{ \"LanguageMessages\": {} }");

    nm_test::RecordingLogger logger;
    ConfigService(&logger).LoadOrCreate(dir.path);

    REQUIRE(CountContaining(logger.errors, "Settings.json") == 1);
    for (const std::string& error : logger.errors) {
        if (!Contains(error, "Settings.json")) continue;
        CHECK(Contains(error, "строка 3"));
        CHECK(Contains(error, "позиция"));
        CHECK(Contains(error, dir.path));
    }
    // И громкая сводка, чтобы это не потерялось в логе
    CHECK(CountContaining(logger.infos, "не удалось прочитать") == 1);
}

TEST_CASE("Битый файл — дефолты только для него") {
    nm_test::TempDir dir;
    Write(dir.path + "/Settings.json", "{ это не json ");
    Write(dir.path + "/Ads.json", "{ \"Ads\": [ { \"Interval\": 42, \"Messages\": [ { \"Chat\": \"hi\" } ] } ] }");

    nm_test::RecordingLogger logger;
    const Config config = ConfigService(&logger).LoadOrCreate(dir.path);

    CHECK(config.defaultLang == "RU");
    REQUIRE(config.ads.size() == 1);
    CHECK(config.ads[0].interval == doctest::Approx(42.0));
}

TEST_CASE("Не тот тип поля отвергает весь файл, как System.Text.Json") {
    nm_test::TempDir dir;
    Write(dir.path + "/Settings.json", "{ \"DefaultLang\": \"PL\", \"Debug\": \"yes\" }");

    nm_test::RecordingLogger logger;
    const Config config = ConfigService(&logger).LoadOrCreate(dir.path);

    CHECK(config.defaultLang == "RU");
    CHECK(CountContaining(logger.errors, "Debug") == 1);
}

TEST_CASE("Пустой файл — ошибка, но не падение") {
    nm_test::TempDir dir;
    Write(dir.path + "/Servers.json", "   ");
    Write(dir.path + "/Settings.json", "{ \"DefaultLang\": \"US\" }");

    nm_test::RecordingLogger logger;
    const Config config = ConfigService(&logger).LoadOrCreate(dir.path);

    CHECK(config.defaultLang == "US");
    bool reported = false;
    for (const std::string& error : logger.errors) {
        reported = reported || (Contains(error, "Servers.json") && Contains(error, "пуст"));
    }
    CHECK(reported);
    CHECK_FALSE(config.servers.has_value());
}

TEST_CASE("Висячие запятые и комментарии разрешены") {
    nm_test::TempDir dir;
    Write(dir.path + "/Settings.json", "{\n  // язык по умолчанию\n  \"DefaultLang\": \"DE\", /* ещё */\n}");

    nm_test::RecordingLogger logger;
    const Config config = ConfigService(&logger).LoadOrCreate(dir.path);

    CHECK(config.defaultLang == "DE");
    CHECK(CountContaining(logger.errors, "Settings.json") == 0);
}

TEST_CASE("Битый конфиг не перезаписывается") {
    nm_test::TempDir dir;
    const std::string broken = "{ \"DefaultLang\": \"RU\" ";
    Write(dir.path + "/Settings.json", broken);

    nm_test::RecordingLogger logger;
    ConfigService(&logger).LoadOrCreate(dir.path);

    CHECK(Read(dir.path + "/Settings.json") == broken);
}

TEST_CASE("Файл с BOM читается") {
    nm_test::TempDir dir;
    Write(dir.path + "/Settings.json", "\xEF\xBB\xBF{ \"DefaultLang\": \"UA\" }");

    nm_test::RecordingLogger logger;
    CHECK(ConfigService(&logger).LoadOrCreate(dir.path).defaultLang == "UA");
}

TEST_CASE("null в DefaultLang — это RU, как ?? в MergeParts") {
    nm_test::TempDir dir;
    Write(dir.path + "/Settings.json", "{ \"DefaultLang\": null }");

    nm_test::RecordingLogger logger;
    CHECK(ConfigService(&logger).LoadOrCreate(dir.path).defaultLang == "RU");
}

TEST_CASE("Реклама крутится по кругу, пустой блок — не ошибка") {
    Advertisement ad;
    ad.messages = {{{"Chat", "first"}}, {{"Chat", "second"}}};
    CHECK(ad.NextMessages()->at(0).second == "first");
    CHECK(ad.NextMessages()->at(0).second == "second");
    CHECK(ad.NextMessages()->at(0).second == "first");

    Advertisement empty;
    CHECK(empty.NextMessages() == nullptr);
}

TEST_CASE("RestartNotify: точная отсечка или DefaultMessage") {
    RestartNotifyConfig notify;
    notify.defaultMessage = "default {SECONDS}";
    notify.thresholds = {{"300", "five minutes"}, {"1", "restarting"}};

    CHECK(*notify.ResolveTemplate(300) == "five minutes");
    CHECK(*notify.ResolveTemplate(1) == "restarting");
    CHECK(*notify.ResolveTemplate(4) == "default {SECONDS}");
    CHECK(*notify.ResolveTemplate(999) == "default {SECONDS}");
    CHECK(*notify.ResolveTemplate(0) == "default {SECONDS}");

    RestartNotifyConfig nothing;
    nothing.defaultMessage.clear();
    CHECK(nothing.ResolveTemplate(60) == nullptr);
}

TEST_CASE("Канал: имя без учёта регистра или число") {
    MessageType type = MessageType::Chat;
    CHECK(ParseMessageType("centerhtml", &type));
    CHECK(type == MessageType::CenterHtml);
    CHECK(ParseMessageType(" Alert ", &type));
    CHECK(type == MessageType::Alert);
    CHECK(ParseMessageType("3", &type));
    CHECK(type == MessageType::Console);
    CHECK_FALSE(ParseMessageType("Chta", &type));
    CHECK_FALSE(ParseMessageType("", &type));
}
