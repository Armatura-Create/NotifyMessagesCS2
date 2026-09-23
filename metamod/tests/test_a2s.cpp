// A2S-ответ приходит по сети от произвольного хоста — недоверенный ввод.
// Порт A2SParsingTests.cs плюс живой опрос через петлю: в C#-целях сокеты давал
// .NET, здесь они свои, и проверить их без сервера CS2 можно только так.
#include "core/a2s.h"
#include "core/server_status_service.h"

#include "doctest.h"

#include <arpa/inet.h>
#include <netinet/in.h>
#include <sys/socket.h>
#include <sys/time.h>
#include <unistd.h>

#include <atomic>
#include <chrono>
#include <cstring>
#include <random>
#include <thread>

using namespace nm;

namespace {

using Bytes = std::vector<uint8_t>;

Bytes BuildInfo(const std::string& name, const std::string& map, uint8_t players, uint8_t maxPlayers, uint8_t bots) {
    Bytes bytes = {0xFF, 0xFF, 0xFF, 0xFF, 0x49, 17};
    const auto str = [&bytes](const std::string& value) {
        bytes.insert(bytes.end(), value.begin(), value.end());
        bytes.push_back(0);
    };
    str(name);
    str(map);
    str("csgo");
    str("CS2");
    bytes.push_back(730 & 0xFF);
    bytes.push_back(730 >> 8);
    bytes.push_back(players);
    bytes.push_back(maxPlayers);
    bytes.push_back(bots);
    return bytes;
}

// UDP-сервер на 127.0.0.1: отвечает challenge, потом ответом, порезанным на части
class FakeServer {
public:
    explicit FakeServer(bool split) : _split(split) {
        _socket = socket(AF_INET, SOCK_DGRAM, 0);
        sockaddr_in address;
        std::memset(&address, 0, sizeof(address));
        address.sin_family = AF_INET;
        address.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
        address.sin_port = 0;
        bind(_socket, reinterpret_cast<sockaddr*>(&address), sizeof(address));
        // Без таймаута поток сервера висел бы в recvfrom вечно, если клиент
        // не дошёл до второго запроса: close() на Linux его не будит
        timeval timeout{3, 0};
        setsockopt(_socket, SOL_SOCKET, SO_RCVTIMEO, &timeout, sizeof(timeout));
        socklen_t length = sizeof(address);
        getsockname(_socket, reinterpret_cast<sockaddr*>(&address), &length);
        port = ntohs(address.sin_port);
        _thread = std::thread([this]() { Serve(); });
    }

    ~FakeServer() {
        if (_thread.joinable()) _thread.join();
        close(_socket);
    }

    uint16_t port = 0;
    std::atomic<int> requests{0};

private:
    void Send(const Bytes& data, const sockaddr_in& to) {
        sendto(_socket, data.data(), data.size(), 0, reinterpret_cast<const sockaddr*>(&to), sizeof(to));
    }

    void Serve() {
        uint8_t buffer[2048];
        for (int round = 0; round < 2; ++round) {
            sockaddr_in from;
            socklen_t length = sizeof(from);
            const ssize_t received =
                recvfrom(_socket, buffer, sizeof(buffer), 0, reinterpret_cast<sockaddr*>(&from), &length);
            if (received <= 0) return;
            ++requests;

            if (round == 0) {
                // Первый запрос — без challenge: требуем его
                Send({0xFF, 0xFF, 0xFF, 0xFF, 0x41, 1, 2, 3, 4}, from);
                continue;
            }

            // Запрос с challenge обязан нести те же 4 байта в конце
            const bool challenged = received >= 4 && buffer[received - 4] == 1 && buffer[received - 1] == 4;
            const Bytes info = BuildInfo(challenged ? "Сервер" : "no challenge", "de_инферно", 12, 32, 2);
            if (!_split) {
                Send(info, from);
                return;
            }

            // Два фрагмента в обратном порядке: сборка обязана разложить их по номерам
            const size_t half = info.size() / 2;
            const Bytes parts[2] = {Bytes(info.begin(), info.begin() + half), Bytes(info.begin() + half, info.end())};
            for (int index = 1; index >= 0; --index) {
                Bytes packet = {0xFE, 0xFF, 0xFF, 0xFF, 0x10, 0x00, 0x00, 0x00, 2, static_cast<uint8_t>(index), 0xE0, 0x04};
                packet.insert(packet.end(), parts[index].begin(), parts[index].end());
                Send(packet, from);
            }
            return;
        }
    }

    int _socket = -1;
    bool _split;
    std::thread _thread;
};

}  // namespace

TEST_CASE("Разбор корректного пакета") {
    const auto info = a2s::ParseInfo(BuildInfo("My Server", "de_dust2", 12, 32, 2));
    REQUIRE(info.has_value());
    CHECK(info->serverName == "My Server");
    CHECK(info->map == "de_dust2");
    CHECK(info->players == 12);
    CHECK(info->maxPlayers == 32);
    CHECK(info->bots == 2);
    CHECK(info->appId == 730);
}

TEST_CASE("UTF-8 имена доходят целыми") {
    const auto info = a2s::ParseInfo(BuildInfo("Сервер Армату́ра", "de_инферно", 1, 10, 0));
    REQUIRE(info.has_value());
    CHECK(info->serverName == "Сервер Армату́ра");
    CHECK(info->map == "de_инферно");
}

TEST_CASE("Любой обрезок пакета отвергается без падения") {
    const Bytes full = BuildInfo("Server", "de_nuke", 5, 10, 0);
    for (size_t length = 0; length < full.size(); ++length) {
        CHECK_FALSE(a2s::ParseInfo(Bytes(full.begin(), full.begin() + length)).has_value());
    }
}

TEST_CASE("Не тот тип ответа и строка без терминатора отвергаются") {
    Bytes packet = BuildInfo("Server", "de_nuke", 5, 10, 0);
    packet[4] = 0x41;
    CHECK_FALSE(a2s::ParseInfo(packet).has_value());

    Bytes open = {0xFF, 0xFF, 0xFF, 0xFF, 0x49, 17};
    const std::string text = "no terminator here";
    open.insert(open.end(), text.begin(), text.end());
    CHECK_FALSE(a2s::ParseInfo(open).has_value());
}

TEST_CASE("Мусор не роняет разбор") {
    std::mt19937 random(1234);
    for (int i = 0; i < 500; ++i) {
        Bytes junk(random() % 64);
        for (uint8_t& b : junk) b = static_cast<uint8_t>(random());
        a2s::ParseInfo(junk);
    }
    CHECK(true);
}

TEST_CASE("Строка до нуля сдвигает указатель за терминатор") {
    const Bytes data = {'a', 'b', 0, 'c', 'd', 0};
    size_t index = 0;
    std::string value;
    CHECK(a2s::ReadNullTerminated(data, &index, &value));
    CHECK(value == "ab");
    CHECK(a2s::ReadNullTerminated(data, &index, &value));
    CHECK(value == "cd");
    CHECK_FALSE(a2s::ReadNullTerminated(data, &index, &value));
}

TEST_CASE("Опрос через петлю: challenge и сборка фрагментов") {
    for (const bool split : {false, true}) {
        CAPTURE(split);
        FakeServer server(split);
        const auto info = a2s::QueryInfo("127.0.0.1", server.port, 1000);
        REQUIRE(info.has_value());
        CHECK(info->serverName == "Сервер");
        CHECK(info->map == "de_инферно");
        CHECK(server.requests == 2);
    }
}

TEST_CASE("Молчащий сервер — nullopt за таймаут, а не зависание") {
    // Свободный порт: заняли и отпустили — на нём никто не слушает
    const int probe = socket(AF_INET, SOCK_DGRAM, 0);
    sockaddr_in address;
    std::memset(&address, 0, sizeof(address));
    address.sin_family = AF_INET;
    address.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    bind(probe, reinterpret_cast<sockaddr*>(&address), sizeof(address));
    socklen_t length = sizeof(address);
    getsockname(probe, reinterpret_cast<sockaddr*>(&address), &length);
    close(probe);

    const auto started = std::chrono::steady_clock::now();
    const auto info = a2s::QueryInfo("127.0.0.1", ntohs(address.sin_port), 200);
    CHECK_FALSE(info.has_value());
    CHECK(std::chrono::steady_clock::now() - started < std::chrono::milliseconds(1000));
}

TEST_CASE("Текст чужого сервера чистится") {
    CHECK(ServerStatusService::SanitizeRemoteText("{prefix}{RED} de_dust2") == "prefixRED de_dust2");
    CHECK(ServerStatusService::SanitizeRemoteText("de_dust2\n\r\t\x01spam\xE2\x80\xA9") == "de_dust2spam");
    CHECK(ServerStatusService::SanitizeRemoteText(std::string(500, 'a')).size() == 64);
    CHECK(ServerStatusService::SanitizeRemoteText("  карта  ") == "карта");
    // Битый UTF-8 не уезжает клиенту как есть
    CHECK(ServerStatusService::SanitizeRemoteText("de\xFF") == "de\xEF\xBF\xBD");
    // Длина — в символах UTF-16, как у string в C#, а не в байтах
    std::string cyrillic;
    for (int i = 0; i < 100; ++i) cyrillic += "ж";
    CHECK(ServerStatusService::SanitizeRemoteText(cyrillic).size() == 128);
}

TEST_CASE("Строки списка серверов: плейсхолдеры, боты вычитаются, офлайн") {
    ServerData data;
    data.ip = "10.0.0.1";
    data.port = 27015;
    data.messageTemplate = "{SERVER_IP}:{SERVER_PORT} {SERVER_MAP} {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}";

    A2SInfo info;
    info.map = "de_dust2";
    info.players = 10;
    info.bots = 4;
    info.maxPlayers = 32;

    const auto lines = ServerStatusService::BuildServerLines(data, info);
    CHECK(lines.first == "10.0.0.1:27015 de_dust2 6/32");
    CHECK(lines.second == lines.first);  // console-шаблон не задан — берётся chat

    ServerData offline;
    offline.messageTemplate = "{SERVER_MAP} {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}";
    offline.maxPlayersFallback = 64;
    CHECK(ServerStatusService::BuildServerLines(offline, std::nullopt).first == "OFFLINE 0/64");

    ServerData bare;
    bare.messageTemplate = "{SERVER_MAP}";
    CHECK(ServerStatusService::BuildServerLines(bare, std::nullopt).first == "OFFLINE");
}
