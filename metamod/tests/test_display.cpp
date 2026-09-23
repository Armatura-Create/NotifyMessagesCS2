// Доставка: канал, кеш по языку, жизнь HTML-сообщения в слоте.
// В C#-целях DisplayService стоит на фреймворке и так не проверялся; здесь его
// логика в ядре. Часть — порт HtmlCenterSlotTests.cs.
#include "core/display_service.h"
#include "core/players.h"
#include "helpers.h"

#include "doctest.h"

#include <map>

using namespace nm;
using nm_test::Contains;

namespace {

struct Stage {
    Config config = ConfigService::BuildDefaultConfig();
    nm_test::FakeServerInfo server;
    PlayerRegistry players;
    nm_test::RecordingSink sink;
    nm_test::RecordingLogger logger;
    std::map<uint64_t, std::string> languages;
    int resolved = 0;
    MessageProcessor processor{&config,
                               [this](uint64_t id) {
                                   ++resolved;
                                   const auto it = languages.find(id);
                                   return it != languages.end() ? it->second : std::string();
                               },
                               &server};
    DisplayService display{&config, &processor, &players, &sink, &logger};

    void Join(int slot, uint64_t steamId, const std::string& lang) {
        players.Connect(slot, steamId, "P" + std::to_string(slot), "", false);
        players.PutInServer(slot, steamId, "P" + std::to_string(slot));
        languages[steamId] = lang;
    }
};

}  // namespace

TEST_CASE("Рассылка идёт каждому на его языке, чат — с префиксом цвета") {
    Stage s;
    s.Join(0, 1, "RU");
    s.Join(1, 2, "US");
    s.Join(2, 3, "RU");

    s.display.Print(MessageType::Chat, "{prefix}{player}");

    REQUIRE(s.sink.sent.size() == 3);
    for (const auto& delivery : s.sink.sent) {
        CHECK(delivery.channel == "Chat");
        CHECK(delivery.text.compare(0, 2, "\x01 ") == 0);
    }
    CHECK(Contains(s.sink.sent[0].text, "Игрок"));
    CHECK(Contains(s.sink.sent[1].text, "Player"));
    CHECK(s.sink.sent[2].text == s.sink.sent[0].text);
}

TEST_CASE("Боты и ещё не вошедшие не получают ничего") {
    Stage s;
    s.players.Connect(0, 0, "Bot", "", true);
    s.players.PutInServer(0, 0, "Bot");
    s.players.Connect(1, 5, "Loading", "", false);  // без ClientPutInServer

    s.display.Print(MessageType::Chat, "hi");
    s.display.Print(MessageType::Chat, "hi", 0);
    s.display.Print(MessageType::Chat, "hi", 1);
    CHECK(s.sink.sent.empty());
}

TEST_CASE("PrintToCenterHtml поднимает Center до HTML, без менеджера событий — наоборот") {
    Stage s;
    s.Join(0, 1, "RU");

    s.config.printToCenterHtml = true;
    s.display.Print(MessageType::Center, "{RED}x", 0);
    s.display.OnTick(0.0);
    REQUIRE(s.sink.sent.size() == 1);
    CHECK(s.sink.sent[0].channel == "CenterHtml");
    CHECK(Contains(s.sink.sent[0].text, "<font"));

    // Панели нет — лучше обычный центр без цветов, чем ничего
    s.sink.sent.clear();
    s.sink.html = false;
    s.display.Print(MessageType::CenterHtml, "{RED}x", 0);
    REQUIRE(s.sink.sent.size() == 1);
    CHECK(s.sink.sent[0].channel == "Center");
    CHECK(s.sink.sent[0].text == "x");
}

TEST_CASE("HTML-сообщение перерисовывается каждый кадр и гаснет по времени") {
    Stage s;
    s.config.htmlCenterDuration = 2.0f;
    s.Join(0, 1, "RU");

    s.display.Print(MessageType::CenterHtml, "hello", 0);
    CHECK(s.sink.sent.empty());  // рисует тик, а не Print

    s.display.OnTick(10.0);
    s.display.OnTick(11.0);
    s.display.OnTick(11.9);
    CHECK(s.sink.sent.size() == 3);

    s.display.OnTick(12.0);
    s.display.OnTick(13.0);
    CHECK(s.sink.sent.size() == 3);
}

TEST_CASE("Сообщение ушедшего игрока не достаётся новому владельцу слота") {
    Stage s;
    s.Join(0, 1, "RU");
    s.display.Print(MessageType::CenterHtml, "for one", 0);
    s.display.OnTick(0.0);
    REQUIRE(s.sink.sent.size() == 1);

    s.players.Disconnect(0);
    s.Join(0, 2, "RU");
    s.sink.sent.clear();
    s.display.OnTick(0.1);
    CHECK(s.sink.sent.empty());
}

TEST_CASE("Новый владелец слота получает своё сообщение, даже если старое не погасло") {
    // Регрессия cssharp: счётчик активных слотов не рос для слота, оставшегося
    // htmlPrint после ушедшего игрока, и OnTick выходил сразу — сообщение терялось
    Stage s;
    s.Join(0, 1, "RU");
    s.display.Print(MessageType::CenterHtml, "old", 0);
    s.display.OnTick(0.0);

    s.players.Disconnect(0);
    s.display.OnTick(0.1);  // пересчёт: активных ноль, слот 0 всё ещё помечен

    s.Join(0, 2, "RU");
    s.sink.sent.clear();
    s.display.Print(MessageType::CenterHtml, "new", 0);
    s.display.OnTick(0.2);
    REQUIRE(s.sink.sent.size() == 1);
    CHECK(s.sink.sent[0].text == "new");
}

TEST_CASE("Пора гасить: время вышло или слот чужой") {
    CHECK_FALSE(DisplayService::ShouldStopShowing(1, 1, 1.0, 5.0));
    CHECK(DisplayService::ShouldStopShowing(1, 1, 5.0, 5.0));
    CHECK(DisplayService::ShouldStopShowing(1, 2, 0.0, 5.0));
    CHECK(DisplayService::ShouldStopShowing(0, 1, 0.0, 5.0));
}

TEST_CASE("Console и Alert уходят как есть") {
    Stage s;
    s.Join(0, 1, "RU");
    s.display.Print(MessageType::Console, "{GREEN}a\nb", 0);
    s.display.Print(MessageType::Alert, "{GREEN}alert", 0);
    REQUIRE(s.sink.sent.size() == 2);
    CHECK(s.sink.sent[0].text == "a\nb");
    CHECK(s.sink.sent[1].channel == "Alert");
    CHECK(s.sink.sent[1].text == "alert");
}
