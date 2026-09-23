// Каналы вывода и путь шаблона до игрока. Порт ChannelRenderTests.cs,
// TextFormatterTests.cs и сквозных проверок ColorPipelineTests / SystemTagTests.
#include "core/config.h"
#include "core/message_processor.h"
#include "core/template_diagnostics.h"
#include "core/text_formatter.h"
#include "helpers.h"

#include "doctest.h"

using namespace nm;
using nm_test::Contains;
using nm_test::HasControlCodes;

namespace {

int Count(const std::string& text, const std::string& part) {
    int count = 0;
    for (size_t at = text.find(part); at != std::string::npos; at = text.find(part, at + part.size())) ++count;
    return count;
}

}  // namespace

TEST_CASE("Chat рисует цвета управляющими байтами") {
    const std::string result = MessageProcessor::Render("{RED}Привет", MessageType::Chat);
    CHECK(HasControlCodes(result));
    CHECK_FALSE(Contains(result, "{RED}"));
}

TEST_CASE("CenterHtml рисует разметкой, а не байтами чата") {
    // Регрессия: в PrintToCenterHtml уходили чат-байты и U+2029
    const std::string result = MessageProcessor::Render("{RED}Строка\nВторая", MessageType::CenterHtml);
    CHECK(Contains(result, "<font color='#FF4040'>"));
    CHECK(Contains(result, "<br>"));
    CHECK_FALSE(HasControlCodes(result));
    CHECK_FALSE(Contains(result, "\xE2\x80\xA9"));
}

TEST_CASE("Center — обычный текст") {
    const std::string result = MessageProcessor::Render("{RED}Привет{DEFAULT}", MessageType::Center);
    CHECK(result == "Привет");
}

TEST_CASE("Console сохраняет настоящий перенос строки") {
    CHECK(MessageProcessor::Render("{GREEN}раз\nдва", MessageType::Console) == "раз\nдва");
}

TEST_CASE("Center переводит \\n в U+2029") {
    CHECK(Contains(MessageProcessor::Render("раз\nдва", MessageType::Center), "\xE2\x80\xA9"));
}

TEST_CASE("Значения подставляются без учёта регистра") {
    const Values values = {{"{COUNTRY}", "Poland"}};
    CHECK(MessageProcessor::ApplyValues("{country}", &values, MessageType::Chat) == "Poland");
}

TEST_CASE("Значения с тегами красятся при рендере") {
    // Регрессия: смена команды показывала литеральное "{RED}Terrorists{DEFAULT}"
    const Values values = {{"{TEAM}", "{RED}Terrorists{DEFAULT}"}};
    const std::string rendered =
        MessageProcessor::Render(MessageProcessor::ApplyValues("перешёл в {TEAM}", &values, MessageType::Chat),
                                 MessageType::Chat);
    CHECK_FALSE(Contains(rendered, "{RED}"));
    CHECK(HasControlCodes(rendered));
}

TEST_CASE("Ник экранируется только для HTML-канала") {
    const Values values = {{"{PLAYERNAME}", "<img src=x>"}};
    CHECK(MessageProcessor::ApplyValues("{PLAYERNAME}", &values, MessageType::CenterHtml) == "&lt;img src=x&gt;");
    CHECK(MessageProcessor::ApplyValues("{PLAYERNAME}", &values, MessageType::Chat) == "<img src=x>");
}

TEST_CASE("Коды цветов — ChatColors CounterStrikeSharp") {
    // Регрессия cssharp до 2.1.0: своя таблица рисовала {BLUE} пурпурным
    CHECK(text::ReplaceColorTags("{BLUE}") == "\x0B");
    CHECK(text::ReplaceColorTags("{YELLOW}") == "\x09");
    CHECK(text::ReplaceColorTags("{GREEN}") == "\x04");
    CHECK(text::ReplaceColorTags("{GREY}") == "\x08");
    CHECK(text::ReplaceColorTags("{red}") == "\x07");
    CHECK(text::ReplaceColorTags("{LIGHTBLUE}") == "\x0B");
    CHECK(text::ReplaceColorTags("{NOT_A_COLOR}") == "{NOT_A_COLOR}");
    CHECK(text::StripColorCodes(text::ReplaceColorTags("{RED}hello{DEFAULT}")) == "hello");
}

TEST_CASE("EnsureChatColorPrefix") {
    const std::string lightBlue = "\x0B";

    // Регрессия: ранний выход «уже начинается с кода» выводил строку белой
    CHECK(text::EnsureChatColorPrefix(lightBlue + "Server") == "\x01 " + lightBlue + "Server");
    CHECK(text::EnsureChatColorPrefix(" " + lightBlue + "Server") == "\x01 " + lightBlue + "Server");

    const std::string once = text::EnsureChatColorPrefix(lightBlue + "Server");
    CHECK(text::EnsureChatColorPrefix(once) == once);

    const std::string middle = text::EnsureChatColorPrefix("Привет " + lightBlue + "мир");
    CHECK(middle.compare(0, 2, "\x01 ") == 0);
    CHECK(Contains(middle, lightBlue + "мир"));

    CHECK(text::EnsureChatColorPrefix("Просто текст") == "Просто текст");
}

TEST_CASE("CenterHtml закрывает каждый <font>, размеры — классы панели") {
    const std::string html = MessageProcessor::Render("{BIG}{RED}Заголовок\n{SMALL}тело", MessageType::CenterHtml);
    CHECK(Count(html, "<font") == 3);
    CHECK(Count(html, "</font>") == 3);
    CHECK(Contains(html, "fontSize-l"));
    CHECK(Contains(html, "fontSize-sm"));
}

TEST_CASE("Размеры в чате вырезаются молча") {
    const std::string chat = MessageProcessor::Render("{BIG}Текст", MessageType::Chat);
    CHECK_FALSE(text::ContainsIgnoreCase(chat, "{BIG}"));
    CHECK(Contains(chat, "Текст"));
}

TEST_CASE("{SPACE} в чате — широкий пробел") {
    CHECK(text::ReplaceColorTags("a{SPACE}b") == "a\xE3\x85\xA4\xE3\x85\xA4\xE3\x85\xA4" "b");
}

namespace {

struct Pipeline {
    Config config = ConfigService::BuildDefaultConfig();
    nm_test::FakeServerInfo server;
    MessageProcessor processor{&config, [](uint64_t) { return std::string("RU"); }, &server};
};

}  // namespace

TEST_CASE("Префикс из дефолтного конфига доезжает до чата цветным") {
    Pipeline p;
    const std::string processed = p.processor.ProcessMessage("{prefix}Текст", 0, MessageType::Chat);
    const std::string forChat = text::EnsureChatColorPrefix(processed);

    CHECK(HasControlCodes(processed));
    CHECK_FALSE(Contains(forChat, "{"));
    CHECK(forChat.compare(0, 3, "\x01 \x0B") == 0);
}

TEST_CASE("Ни один известный тег не доезжает до игрока скобками ни в одном канале") {
    Pipeline p;
    int count = 0;
    const char* const* tags = text::KnownColorTagList(&count);
    const MessageType channels[] = {MessageType::Chat, MessageType::Center, MessageType::CenterHtml,
                                    MessageType::Console, MessageType::Alert};
    for (int i = 0; i < count; ++i) {
        for (const MessageType channel : channels) {
            const std::string result = p.processor.ProcessMessage(std::string(tags[i]) + "X", 0, channel);
            CAPTURE(tags[i]);
            CHECK_FALSE(text::ContainsIgnoreCase(result, tags[i]));
        }
    }
}

TEST_CASE("Опечатка в теге остаётся текстом и ловится проверкой") {
    Pipeline p;
    CHECK(Contains(p.processor.ProcessMessage("{LIGHTPBLUE}X", 0, MessageType::Chat), "{LIGHTPBLUE}"));

    bool reported = false;
    for (const TemplateIssue& issue : AnalyzeTemplate("{LIGHTPBLUE}", p.config, "test")) {
        reported = reported || (issue.severity == TemplateSeverity::Error && issue.tag == "{LIGHTPBLUE}");
    }
    CHECK(reported);
}

TEST_CASE("Системные теги берутся из IServerInfoSource, имена карт — из MapsName") {
    Pipeline p;
    p.server.hostname = "Armaturix";
    p.server.players = 12;
    p.server.maxPlayers = 24;
    CHECK(p.processor.ProcessMessage("{SERVERNAME}: {PLAYERS}/{MAXPLAYERS} на {MAP}", 0, MessageType::Console) ==
          "Armaturix: 12/24 на Dust 2");
}

TEST_CASE("Имя карты с долларом не считается шаблоном замены") {
    Config config;
    config.mapsName = {{"de_cash", "Cash $$$ Map"}};
    nm_test::FakeServerInfo server;
    server.map = "de_cash";
    MessageProcessor processor(&config, [](uint64_t) { return std::string("RU"); }, &server);
    CHECK(processor.ProcessMessage("{MAP}", 0, MessageType::Console) == "Cash $$$ Map");
}

TEST_CASE("Имя карты заменяется только целым словом") {
    Config config;
    config.mapsName = {{"de_dust2", "Dust 2"}};
    nm_test::FakeServerInfo server;
    MessageProcessor processor(&config, [](uint64_t) { return std::string("RU"); }, &server);

    CHECK(processor.ReplaceMessageTags("карта de_dust2, ура") == "карта Dust 2, ура");
    CHECK(processor.ReplaceMessageTags("xde_dust2") == "xde_dust2");
    CHECK(processor.ReplaceMessageTags("de_dust2_night") == "de_dust2_night");
    // Стрелка — не буква, как и в \b .NET
    CHECK(processor.ReplaceMessageTags("➡de_dust2") == "➡Dust 2");
}

TEST_CASE("Язык: блок получателя, иначе DefaultLang") {
    Config config = ConfigService::BuildDefaultConfig();
    nm_test::FakeServerInfo server;
    MessageProcessor english(&config, [](uint64_t) { return std::string("US"); }, &server);
    MessageProcessor unknown(&config, [](uint64_t) { return std::string("ZZ"); }, &server);

    CHECK(english.ProcessMessage("{player}", 1, MessageType::Console) == "Player");
    CHECK(unknown.ProcessMessage("{player}", 1, MessageType::Console) == "Игрок");
    // steamId 0 — язык по умолчанию без вызова резолвера
    CHECK(english.ProcessMessage("{player}", 0, MessageType::Console) == "Игрок");
}
