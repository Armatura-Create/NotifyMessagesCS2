#include "core/a2s.h"

#include <algorithm>
#include <chrono>
#include <cstring>
#include <map>

#ifdef _WIN32
#include <winsock2.h>
#include <ws2tcpip.h>
#else
#include <netdb.h>
#include <netinet/in.h>
#include <poll.h>
#include <sys/socket.h>
#include <sys/types.h>
#include <unistd.h>
#endif

namespace nm {
namespace a2s {
namespace {

using SteadyClock = std::chrono::steady_clock;
using Bytes = std::vector<uint8_t>;

// https://developer.valvesoftware.com/wiki/Server_queries#A2S_INFO
constexpr uint8_t kHeader[] = {0xFF, 0xFF, 0xFF, 0xFF, 0x54};  // 'T'
constexpr char kQuery[] = "Source Engine Query";                 // + '\0'

// 4 (0xFFFFFFFE) + 4 (ID) + 1 (total) + 1 (number) + 2 (splitSize) — Source-формат
constexpr size_t kSplitHeaderSize = 12;

// Здравые пределы: кривой или злонамеренный ответ не заставит копить память
constexpr int kMaxSplitPackets = 32;
constexpr size_t kMaxReassembledBytes = 256 * 1024;

#ifdef _WIN32
using Socket = SOCKET;
const Socket kInvalidSocket = INVALID_SOCKET;
void CloseSocket(Socket s) { closesocket(s); }
#else
using Socket = int;
const Socket kInvalidSocket = -1;
void CloseSocket(Socket s) { close(s); }
#endif

bool CanRead(const Bytes& data, size_t index, size_t count) { return index + count <= data.size(); }

int32_t ReadInt32(const Bytes& data, size_t at) {
    return static_cast<int32_t>(static_cast<uint32_t>(data[at]) | (static_cast<uint32_t>(data[at + 1]) << 8) |
                                (static_cast<uint32_t>(data[at + 2]) << 16) |
                                (static_cast<uint32_t>(data[at + 3]) << 24));
}

bool HasPrefix(const Bytes& data, uint8_t type) {
    return data.size() >= 5 && data[0] == 0xFF && data[1] == 0xFF && data[2] == 0xFF && data[3] == 0xFF &&
           data[4] == type;
}

bool IsChallenge(const Bytes& data) { return HasPrefix(data, 0x41); }  // 'A'

// Заголовок split-пакета — int32 -2, то есть байты FE FF FF FF. C#-цель проверяет
// FF FF FF FF FE и потому фрагментированный ответ не собирает никогда (ответ CS2
// на A2S_INFO обычно влезает в один пакет, так что это почти не видно).
bool IsSplit(const Bytes& data) {
    return data.size() >= kSplitHeaderSize && data[0] == 0xFE && data[1] == 0xFF && data[2] == 0xFF &&
           data[3] == 0xFF;
}

struct Fragment {
    int32_t id = 0;
    uint8_t total = 0;
    uint8_t index = 0;
    Bytes payload;
};

bool ReadFragment(const Bytes& data, Fragment* out) {
    if (data.size() < kSplitHeaderSize) return false;
    out->id = ReadInt32(data, 4);
    // Старший бит ID — сжатый ответ (bzip2). CS2 такое не шлёт, разбирать не умеем
    if ((static_cast<uint32_t>(out->id) & 0x80000000u) != 0) return false;
    out->total = data[8];
    out->index = data[9];
    out->payload.assign(data.begin() + kSplitHeaderSize, data.end());
    return true;
}

bool SameAddress(const sockaddr_storage& a, const sockaddr_storage& b) {
    if (a.ss_family != b.ss_family) return false;
    if (a.ss_family == AF_INET) {
        return std::memcmp(&reinterpret_cast<const sockaddr_in&>(a).sin_addr,
                           &reinterpret_cast<const sockaddr_in&>(b).sin_addr, sizeof(in_addr)) == 0;
    }
    if (a.ss_family == AF_INET6) {
        return std::memcmp(&reinterpret_cast<const sockaddr_in6&>(a).sin6_addr,
                           &reinterpret_cast<const sockaddr_in6&>(b).sin6_addr, sizeof(in6_addr)) == 0;
    }
    return false;
}

// ponytail: getaddrinfo без таймаута — hostname с мёртвым DNS держит фоновый поток
// на время системного резолвера. C# ограничивал резолв двумя секундами; IP в
// Servers.json резолвится мгновенно, так что это касается только имён хостов.
bool Resolve(const std::string& host, uint16_t port, sockaddr_storage* out, int* length) {
    addrinfo hints;
    std::memset(&hints, 0, sizeof(hints));
    hints.ai_family = AF_UNSPEC;
    hints.ai_socktype = SOCK_DGRAM;
    hints.ai_protocol = IPPROTO_UDP;

    addrinfo* found = nullptr;
    if (getaddrinfo(host.c_str(), std::to_string(port).c_str(), &hints, &found) != 0 || found == nullptr) {
        return false;
    }

    bool ok = false;
    for (addrinfo* it = found; it != nullptr; it = it->ai_next) {
        if ((it->ai_family == AF_INET || it->ai_family == AF_INET6) &&
            it->ai_addrlen <= sizeof(sockaddr_storage)) {
            std::memset(out, 0, sizeof(*out));
            std::memcpy(out, it->ai_addr, it->ai_addrlen);
            *length = static_cast<int>(it->ai_addrlen);
            ok = true;
            break;
        }
    }
    freeaddrinfo(found);
    return ok;
}

class Query {
public:
    Query(Socket socket, const sockaddr_storage& target, int targetLength, SteadyClock::time_point deadline)
        : _socket(socket), _target(target), _targetLength(targetLength), _deadline(deadline) {}

    bool Send(const Bytes& data) {
        const auto sent = sendto(_socket, reinterpret_cast<const char*>(data.data()), static_cast<int>(data.size()),
                                 0, reinterpret_cast<const sockaddr*>(&_target), _targetLength);
        return sent >= 0 && static_cast<size_t>(sent) == data.size();
    }

    // Одна датаграмма от опрашиваемого адреса или ничего. Ответы с чужих адресов
    // отбрасываются: иначе любой посторонний хост мог подменить статус сервера.
    bool Receive(int timeoutMs, Bytes* out) {
        const auto until = std::min(_deadline, SteadyClock::now() + std::chrono::milliseconds(timeoutMs));
        Bytes buffer(65536);

        while (true) {
            const auto remaining =
                std::chrono::duration_cast<std::chrono::milliseconds>(until - SteadyClock::now()).count();
            if (remaining <= 0) return false;
            if (!Wait(static_cast<int>(remaining))) return false;

            sockaddr_storage from;
            socklen_t fromLength = sizeof(from);
            const auto received = recvfrom(_socket, reinterpret_cast<char*>(buffer.data()),
                                           static_cast<int>(buffer.size()), 0,
                                           reinterpret_cast<sockaddr*>(&from), &fromLength);
            // Windows отдаёт ICMP «порт недоступен» ошибкой recvfrom — это «офлайн»
            if (received < 0) return false;
            if (!SameAddress(from, _target)) continue;

            out->assign(buffer.begin(), buffer.begin() + received);
            return true;
        }
    }

private:
    bool Wait(int timeoutMs) {
#ifdef _WIN32
        // select на Windows не ограничен номером сокета
        fd_set set;
        FD_ZERO(&set);
        FD_SET(_socket, &set);
        timeval tv;
        tv.tv_sec = timeoutMs / 1000;
        tv.tv_usec = (timeoutMs % 1000) * 1000;
        return select(0, &set, nullptr, nullptr, &tv) > 0;
#else
        // poll, а не select: у игрового процесса легко больше 1024 дескрипторов,
        // и FD_SET за FD_SETSIZE — запись за пределы структуры
        pollfd entry;
        entry.fd = _socket;
        entry.events = POLLIN;
        entry.revents = 0;
        return poll(&entry, 1, timeoutMs) > 0 && (entry.revents & POLLIN) != 0;
#endif
    }

    Socket _socket;
    sockaddr_storage _target;
    int _targetLength;
    SteadyClock::time_point _deadline;
};

bool CollectSplit(const Bytes& first, Query& query, int timeoutMs, Bytes* out) {
    Fragment head;
    if (!ReadFragment(first, &head)) return false;
    if (head.total == 0 || head.total > kMaxSplitPackets) return false;

    std::map<uint8_t, Bytes> fragments;
    size_t totalBytes = head.payload.size();
    fragments[head.index] = head.payload;

    const auto deadline = SteadyClock::now() + std::chrono::milliseconds(timeoutMs > 200 ? timeoutMs : 200);
    const int stepMs = timeoutMs / 2 > 50 ? timeoutMs / 2 : 50;

    while (fragments.size() < head.total && SteadyClock::now() < deadline) {
        Bytes data;
        if (!query.Receive(stepMs, &data)) break;
        if (!IsSplit(data)) continue;

        Fragment next;
        if (!ReadFragment(data, &next)) continue;
        // Фрагменты чужого ответа не подмешиваем
        if (next.id != head.id || next.index >= head.total || fragments.count(next.index) != 0) continue;

        totalBytes += next.payload.size();
        if (totalBytes > kMaxReassembledBytes) return false;
        fragments[next.index] = next.payload;
    }

    // Неполная сборка дала бы мусор при разборе — честнее считать сервер недоступным
    out->clear();
    for (uint8_t i = 0; i < head.total; ++i) {
        const auto fragment = fragments.find(i);
        if (fragment == fragments.end()) return false;
        out->insert(out->end(), fragment->second.begin(), fragment->second.end());
    }
    return true;
}

#ifdef _WIN32
// WSAStartup считает ссылки и безопасен из любого потока: пара на каждый опрос
// избавляет от статического флага, который в сборке с -fno-threadsafe-statics
// был бы гонкой
struct WinsockScope {
    bool ok;
    WinsockScope() {
        WSADATA data;
        ok = WSAStartup(MAKEWORD(2, 2), &data) == 0;
    }
    ~WinsockScope() {
        if (ok) WSACleanup();
    }
};
#endif

}  // namespace

bool ReadNullTerminated(const Bytes& data, size_t* index, std::string* out) {
    if (*index >= data.size()) return false;

    size_t end = *index;
    while (end < data.size() && data[end] != 0) ++end;
    if (end >= data.size()) return false;  // нет завершающего нуля — пакет усечён

    out->assign(reinterpret_cast<const char*>(data.data()) + *index, end - *index);
    *index = end + 1;
    return true;
}

std::optional<A2SInfo> ParseInfo(const Bytes& raw) {
    // 0xFF FF FF FF + тип ответа
    if (!CanRead(raw, 0, 5)) return std::nullopt;
    if (raw[4] != 0x49) return std::nullopt;  // 'I'

    A2SInfo info;
    size_t index = 5;

    if (!CanRead(raw, index, 1)) return std::nullopt;
    info.protocol = raw[index++];

    if (!ReadNullTerminated(raw, &index, &info.serverName)) return std::nullopt;
    if (!ReadNullTerminated(raw, &index, &info.map)) return std::nullopt;
    if (!ReadNullTerminated(raw, &index, &info.gameDir)) return std::nullopt;
    if (!ReadNullTerminated(raw, &index, &info.gameDesc)) return std::nullopt;

    if (!CanRead(raw, index, 2)) return std::nullopt;
    info.appId = static_cast<int16_t>(raw[index] | (raw[index + 1] << 8));
    index += 2;

    if (!CanRead(raw, index, 3)) return std::nullopt;
    info.players = raw[index];
    info.maxPlayers = raw[index + 1];
    info.bots = raw[index + 2];

    // Дальше ServerType/Environment/Visibility/VAC и опциональный EDF — не нужны
    return info;
}

std::optional<A2SInfo> QueryInfo(const std::string& host, uint16_t port, int timeoutMs) {
    if (host.empty()) return std::nullopt;

#ifdef _WIN32
    WinsockScope winsock;
    if (!winsock.ok) return std::nullopt;
#endif

    sockaddr_storage target;
    int targetLength = 0;
    if (!Resolve(host, port, &target, &targetLength)) return std::nullopt;

    const Socket sock = socket(target.ss_family, SOCK_DGRAM, IPPROTO_UDP);
    if (sock == kInvalidSocket) return std::nullopt;

    // Весь опрос, включая challenge, укладывается в timeout + 250 мс — как
    // CancellationTokenSource(timeoutMs + 250) в C#
    Query query(sock, target, targetLength,
                SteadyClock::now() + std::chrono::milliseconds(timeoutMs + 250));

    Bytes request(kHeader, kHeader + sizeof(kHeader));
    request.insert(request.end(), kQuery, kQuery + sizeof(kQuery));  // вместе с '\0'

    std::optional<A2SInfo> result;
    Bytes data;

    if (query.Send(request) && query.Receive(timeoutMs, &data)) {
        bool ok = true;

        // challenge: 0xFFFFFFFF 'A' + 4 байта, которые надо дописать к запросу
        if (IsChallenge(data)) {
            ok = data.size() > 5;
            if (ok) {
                Bytes retry = request;
                retry.insert(retry.end(), data.begin() + 5, data.end());
                ok = query.Send(retry) && query.Receive(timeoutMs, &data);
            }
        }

        if (ok && IsSplit(data)) {
            Bytes combined;
            ok = CollectSplit(data, query, timeoutMs, &combined);
            data.swap(combined);
        }

        if (ok) result = ParseInfo(data);
    }

    CloseSocket(sock);
    return result;
}

}  // namespace a2s
}  // namespace nm
