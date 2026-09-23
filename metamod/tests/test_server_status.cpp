// Опрос серверов без сети: кеш, порядок, TTL, остановка. В C#-целях этого нет —
// там поток давал Task.Run; здесь поток свой, и его жизнь — наша ответственность.
#include "core/server_status_service.h"
#include "helpers.h"

#include "doctest.h"

#include <atomic>
#include <chrono>
#include <mutex>
#include <thread>

using namespace nm;

namespace {

ServersConfig TwoServers() {
    ServersConfig servers;
    servers.enabled = true;
    servers.cacheTtlSeconds = 30;
    ServerData first;
    first.ip = "1.1.1.1";
    first.port = 1;
    first.messageTemplate = "first {SERVER_MAP}";
    ServerData second;
    second.ip = "2.2.2.2";
    second.port = 2;
    second.messageTemplate = "second {SERVER_MAP}";
    servers.list = {first, second};
    return servers;
}

struct Harness {
    nm_test::RecordingLogger logger;
    std::atomic<int> queries{0};
    std::atomic<double> now{0};  // часы читает фоновый поток
};

void WaitFor(const std::function<bool()>& done) {
    for (int i = 0; i < 200 && !done(); ++i) std::this_thread::sleep_for(std::chrono::milliseconds(5));
}

}  // namespace

TEST_CASE("Первичный опрос заполняет кеш в порядке Servers.json") {
    Harness h;
    ServerStatusService service(TwoServers(), &h.logger,
                                [](float, std::function<void()>) { return std::function<void()>(); },
                                [](std::function<void()>) {}, [&h]() { return h.now.load(); },
                                [&h](const std::string& host, uint16_t, int) -> std::optional<A2SInfo> {
                                    ++h.queries;
                                    if (host == "2.2.2.2") return std::nullopt;
                                    A2SInfo info;
                                    info.map = "de_mirage";
                                    return info;
                                });

    service.InitialQuery();
    WaitFor([&]() { return service.GetSnapshot().size() == 2; });

    const auto snapshot = service.GetSnapshot();
    REQUIRE(snapshot.size() == 2);
    CHECK(snapshot[0].chat == "first de_mirage");
    CHECK(snapshot[0].online);
    CHECK(snapshot[1].chat == "second OFFLINE");
    CHECK_FALSE(snapshot[1].online);

    service.Stop();
}

TEST_CASE("TTL: свежий кеш не опрашивается повторно") {
    Harness h;
    ServerStatusService service(TwoServers(), &h.logger,
                                [](float, std::function<void()>) { return std::function<void()>(); },
                                [](std::function<void()>) {}, [&h]() { return h.now.load(); },
                                [&h](const std::string&, uint16_t, int) -> std::optional<A2SInfo> {
                                    ++h.queries;
                                    return std::nullopt;
                                });

    service.InitialQuery();
    WaitFor([&]() { return service.GetSnapshot().size() == 2; });
    WaitFor([&]() { return h.queries == 2; });
    std::this_thread::sleep_for(std::chrono::milliseconds(20));

    service.TriggerBackgroundUpdate();
    std::this_thread::sleep_for(std::chrono::milliseconds(50));
    CHECK(h.queries == 2);

    h.now = 31;
    service.TriggerBackgroundUpdate();
    WaitFor([&]() { return h.queries == 4; });
    CHECK(h.queries == 4);
}

TEST_CASE("Выключенный мониторинг и пустой конфиг не опрашивают ничего") {
    Harness h;
    ServersConfig off = TwoServers();
    off.enabled = false;
    ServerStatusService disabled(off, &h.logger, [](float, std::function<void()>) { return std::function<void()>(); },
                                 [](std::function<void()>) {}, [&h]() { return h.now.load(); },
                                 [&h](const std::string&, uint16_t, int) -> std::optional<A2SInfo> {
                                     ++h.queries;
                                     return std::nullopt;
                                 });
    disabled.InitialQuery();
    disabled.TriggerBackgroundUpdate();

    ServerStatusService missing(std::nullopt, &h.logger,
                                [](float, std::function<void()>) { return std::function<void()>(); },
                                [](std::function<void()>) {}, [&h]() { return h.now.load(); });
    missing.InitialQuery();
    CHECK(missing.GetSnapshot().empty());

    std::this_thread::sleep_for(std::chrono::milliseconds(20));
    CHECK(h.queries == 0);
}

TEST_CASE("Остановка ждёт фоновый проход, логи уходят в главный поток") {
    Harness h;
    std::atomic<bool> release{false};
    std::mutex postedMutex;
    std::vector<std::function<void()>> posted;
    {
        ServerStatusService service(
            TwoServers(), &h.logger, [](float, std::function<void()>) { return std::function<void()>(); },
            [&](std::function<void()> task) {
                std::lock_guard<std::mutex> guard(postedMutex);
                posted.push_back(std::move(task));
            },
            [&h]() { return h.now.load(); },
            [&](const std::string&, uint16_t, int) -> std::optional<A2SInfo> {
                while (!release) std::this_thread::sleep_for(std::chrono::milliseconds(1));
                return std::nullopt;
            });
        service.InitialQuery();
        std::thread unlock([&]() {
            std::this_thread::sleep_for(std::chrono::milliseconds(30));
            release = true;
        });
        service.Stop();  // обязан дождаться потока, а не бросить его
        unlock.join();
    }

    // Фоновый поток ни разу не звал логгер сам — только через очередь главного
    for (const std::string& line : h.logger.debugs) CHECK_FALSE(nm_test::Contains(line, "OFFLINE"));
    std::lock_guard<std::mutex> guard(postedMutex);
    CHECK_FALSE(posted.empty());
}

TEST_CASE("Таймаут и TTL: границы как в C#") {
    Harness h;
    ServersConfig servers = TwoServers();
    servers.queryTimeoutMs = 9000;
    servers.cacheTtlSeconds = 120;
    ServerStatusService service(servers, &h.logger, [](float, std::function<void()>) { return std::function<void()>(); },
                                [](std::function<void()>) {}, [&h]() { return h.now.load(); });
    CHECK(service.TimeoutMs() == 500);
    CHECK(service.CacheTtlSeconds() == 5);

    servers.queryTimeoutMs = std::nullopt;
    servers.cacheTtlSeconds = 0;
    ServerStatusService other(servers, &h.logger, [](float, std::function<void()>) { return std::function<void()>(); },
                              [](std::function<void()>) {}, [&h]() { return h.now.load(); });
    CHECK(other.TimeoutMs() == 500);
    CHECK(other.CacheTtlSeconds() == 0);
}
