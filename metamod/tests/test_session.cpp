// Смена сторон, возврат после смены карты, таймеры. Порт TeamSwapTests.cs
// и того, чего в C#-целях нет: планировщика, который там даёт фреймворк.
#include "core/players.h"
#include "core/scheduler.h"
#include "core/session_service.h"

#include "doctest.h"

using namespace nm;

TEST_CASE("Две и больше смены T<->CT за кадр — смена сторон") {
    CHECK(team_swap::IsMassSwap({{2, 3}, {3, 2}, {2, 3}, {3, 2}}));
    CHECK(team_swap::IsMassSwap({{2, 3}, {3, 2}}));
    CHECK_FALSE(team_swap::IsMassSwap({{2, 3}}));
    CHECK_FALSE(team_swap::IsMassSwap({}));
    // Кто-то вышел из спектаторов в тот же кадр — это не смена сторон
    CHECK_FALSE(team_swap::IsMassSwap({{2, 3}, {1, 2}}));
    CHECK_FALSE(team_swap::IsMassSwap({{2, 3}, {0, 3}}));
}

TEST_CASE("Метка возврата: одноразовая, со сроком, чистится выходом") {
    SessionService session;

    session.MarkReturning(1, 100.0);
    CHECK(session.TakeReturning(1, 105.0, 90.0));
    CHECK_FALSE(session.TakeReturning(1, 106.0, 90.0));

    session.MarkReturning(1, 100.0);
    CHECK_FALSE(session.TakeReturning(1, 400.0, 90.0));

    CHECK_FALSE(session.TakeReturning(42, 100.0, 90.0));

    session.MarkReturning(1, 100.0);
    session.RemoveFullyConnected(1);
    CHECK_FALSE(session.TakeReturning(1, 100.0, 90.0));
}

TEST_CASE("Таймер подключения останавливается ровно один раз") {
    SessionService session;
    int stopped = 0;

    session.SetConnectionTimer(7, [&]() { ++stopped; });
    // Второй таймер для того же игрока гасит первый
    session.SetConnectionTimer(7, [&]() { ++stopped; });
    CHECK(stopped == 1);

    CHECK(session.TryKillAndRemoveConnectionTimer(7));
    CHECK(stopped == 2);
    CHECK_FALSE(session.TryKillAndRemoveConnectionTimer(7));
}

TEST_CASE("Язык: пустой не запоминается") {
    SessionService session;
    session.SetLanguage(1, "  ");
    CHECK(session.GetLanguage(1).empty());
    session.SetLanguage(1, "ru");
    CHECK(session.GetLanguage(1) == "ru");
    session.RemoveLanguage(1);
    CHECK(session.GetLanguage(1).empty());
}

TEST_CASE("Планировщик: задержка, повтор, отмена, следующий кадр") {
    double now = 0;
    Scheduler scheduler([&]() { return now; });

    int once = 0;
    int repeated = 0;
    int posted = 0;

    scheduler.Delay(3.0, [&]() { ++once; });
    const Scheduler::Id repeat = scheduler.Repeat(2.0, [&]() { ++repeated; });
    scheduler.NextFrame([&]() { ++posted; });

    scheduler.RunFrame();
    CHECK(posted == 1);
    CHECK(repeated == 0);  // первый показ рекламы — через интервал, а не сразу

    now = 2.0;
    scheduler.RunFrame();
    CHECK(repeated == 1);
    CHECK(once == 0);

    now = 3.0;
    scheduler.RunFrame();
    CHECK(once == 1);

    now = 4.0;
    scheduler.RunFrame();
    CHECK(repeated == 2);

    scheduler.Cancel(repeat);
    now = 10.0;
    scheduler.RunFrame();
    CHECK(repeated == 2);
    CHECK(once == 1);
}

TEST_CASE("Задача может отменить соседнюю в том же кадре") {
    double now = 0;
    Scheduler scheduler([&]() { return now; });
    int second = 0;

    Scheduler::Id victim = 0;
    scheduler.Delay(1.0, [&]() { scheduler.Cancel(victim); });
    victim = scheduler.Delay(1.0, [&]() { ++second; });

    now = 1.0;
    scheduler.RunFrame();
    CHECK(second == 0);
}

TEST_CASE("Реестр игроков: люди в игре, поиск по SteamID, переиспользование слота") {
    PlayerRegistry players;
    players.Connect(0, 111, "Alice", "1.2.3.4", false);
    players.Connect(1, 0, "Bot", "", true);
    players.PutInServer(1, 0, "Bot");

    // До ClientPutInServer человек ещё не получатель
    CHECK(players.Humans().empty());
    CHECK(players.FindBySteamId(111) == nullptr);

    players.PutInServer(0, 111, "Alice");
    REQUIRE(players.Humans().size() == 1);
    CHECK(players.FindBySteamId(111)->slot == 0);
    CHECK(players.CountInGame() == 2);  // боты тоже игроки для {PLAYERS}

    // Слот ушедшего игрока достаётся другому — старый SteamID больше не находится
    players.Disconnect(0);
    players.Connect(0, 222, "Bob", "5.6.7.8", false);
    players.PutInServer(0, 222, "Bob");
    CHECK(players.FindBySteamId(111) == nullptr);
    CHECK(players.Find(0)->name == "Bob");
}
