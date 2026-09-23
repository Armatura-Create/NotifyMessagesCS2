#include "core/server_status_service.h"

#include "core/logger.h"
#include "core/text_formatter.h"

#include <cstdint>
#include <utility>

namespace nm {
namespace {

// Сколько серверов опрашиваем одновременно
constexpr size_t kMaxConcurrentQueries = 8;

// Длиннее этого имя карты не бывает, а всё, что длиннее, — попытка забить чат
constexpr size_t kMaxRemoteTextLength = 64;

// Декодирование UTF-8; битая последовательность — U+FFFD на один байт,
// как Encoding.UTF8.GetString в C#.
std::vector<uint32_t> DecodeUtf8(const std::string& s) {
    std::vector<uint32_t> out;
    size_t i = 0;
    while (i < s.size()) {
        const unsigned char b0 = static_cast<unsigned char>(s[i]);
        int extra = 0;
        uint32_t cp = b0;
        uint32_t min = 0;
        if (b0 >= 0xF0 && b0 <= 0xF4) {
            extra = 3;
            cp = b0 & 0x07;
            min = 0x10000;
        } else if (b0 >= 0xE0 && b0 <= 0xEF) {
            extra = 2;
            cp = b0 & 0x0F;
            min = 0x800;
        } else if (b0 >= 0xC2 && b0 <= 0xDF) {
            extra = 1;
            cp = b0 & 0x1F;
            min = 0x80;
        } else if (b0 >= 0x80) {
            out.push_back(0xFFFD);
            ++i;
            continue;
        }

        bool ok = i + static_cast<size_t>(extra) < s.size();
        for (int k = 1; ok && k <= extra; ++k) {
            const unsigned char b = static_cast<unsigned char>(s[i + k]);
            if ((b & 0xC0) != 0x80) {
                ok = false;
                break;
            }
            cp = (cp << 6) | (b & 0x3F);
        }
        if (!ok || cp < min || cp > 0x10FFFF || (cp >= 0xD800 && cp <= 0xDFFF)) {
            out.push_back(0xFFFD);
            ++i;
            continue;
        }
        out.push_back(cp);
        i += static_cast<size_t>(extra) + 1;
    }
    return out;
}

void AppendUtf8(uint32_t cp, std::string* out) {
    if (cp < 0x80) {
        out->push_back(static_cast<char>(cp));
    } else if (cp < 0x800) {
        out->push_back(static_cast<char>(0xC0 | (cp >> 6)));
        out->push_back(static_cast<char>(0x80 | (cp & 0x3F)));
    } else if (cp < 0x10000) {
        out->push_back(static_cast<char>(0xE0 | (cp >> 12)));
        out->push_back(static_cast<char>(0x80 | ((cp >> 6) & 0x3F)));
        out->push_back(static_cast<char>(0x80 | (cp & 0x3F)));
    } else {
        out->push_back(static_cast<char>(0xF0 | (cp >> 18)));
        out->push_back(static_cast<char>(0x80 | ((cp >> 12) & 0x3F)));
        out->push_back(static_cast<char>(0x80 | ((cp >> 6) & 0x3F)));
        out->push_back(static_cast<char>(0x80 | (cp & 0x3F)));
    }
}

// char.IsControl в .NET
bool IsControl(uint32_t cp) { return cp < 0x20 || (cp >= 0x7F && cp <= 0x9F); }

// Пробельные символы, которые убирает String.Trim()
bool IsWhiteSpace(uint32_t cp) {
    return cp == 0x20 || (cp >= 0x09 && cp <= 0x0D) || cp == 0x85 || cp == 0xA0 || cp == 0x1680 ||
           (cp >= 0x2000 && cp <= 0x200A) || cp == 0x2028 || cp == 0x2029 || cp == 0x202F || cp == 0x205F ||
           cp == 0x3000;
}

std::string Fill(const std::string& templateText, const ServerData& server, const std::string& map,
                 const std::string& players, const std::string& max) {
    std::string result = text::ReplaceOrdinal(templateText, "{SERVER_IP}", server.ip);
    result = text::ReplaceOrdinal(result, "{SERVER_PORT}", std::to_string(server.port));
    result = text::ReplaceOrdinal(result, "{SERVER_MAP}", map);
    result = text::ReplaceOrdinal(result, "{SERVER_PLAYERS}", players);
    return text::ReplaceOrdinal(result, "{SERVER_MAXPLAYERS}", max);
}

}  // namespace

ServerStatusService::ServerStatusService(const std::optional<ServersConfig>& servers, ILogger* logger,
                                         RepeatEvery repeatEvery, RunOnMainThread runOnMainThread, Clock clock,
                                         QueryFunction query)
    : _servers(servers),
      _logger(logger),
      _repeatEvery(std::move(repeatEvery)),
      _runOnMainThread(std::move(runOnMainThread)),
      _clock(std::move(clock)),
      _query(std::move(query)) {}

ServerStatusService::~ServerStatusService() { Stop(); }

bool ServerStatusService::Enabled() const { return _servers && _servers->enabled && !_servers->list.empty(); }

int ServerStatusService::TimeoutMs() const {
    const std::optional<int> value = _servers ? _servers->queryTimeoutMs : std::optional<int>();
    return value && *value > 0 && *value <= 5000 ? *value : 500;
}

int ServerStatusService::CacheTtlSeconds() const {
    const std::optional<int> value = _servers ? _servers->cacheTtlSeconds : std::optional<int>();
    return value && *value >= 0 && *value <= 60 ? *value : 5;
}

void ServerStatusService::BgDebug(const std::string& message) {
    // Логгер пишет в консоль движка — из чужого потока её не трогаем
    ILogger* logger = _logger;
    _runOnMainThread([logger, message]() { logger->Debug(message); });
}

void ServerStatusService::InitialQuery() {
    if (!_servers) {
        _logger->Debug("[ServerStatus] InitialQuery skipped: Servers config is null");
        return;
    }
    if (!_servers->enabled) {
        _logger->Debug("[ServerStatus] InitialQuery skipped: Servers.Enabled = false");
        return;
    }
    if (_servers->list.empty()) {
        _logger->Debug("[ServerStatus] InitialQuery skipped: Servers.List is empty");
        return;
    }

    _logger->Debug("[ServerStatus] InitialQuery started for " + std::to_string(_servers->list.size()) +
                   " server(s)");
    RefreshAsync(true, "Initial query");
}

void ServerStatusService::Start() {
    if (!Enabled()) return;

    const float interval = _servers->interval > 5.0f ? _servers->interval : 5.0f;
    _stopTimers.push_back(_repeatEvery(interval, [this]() { RefreshAsync(false, "Periodic update"); }));
}

void ServerStatusService::Stop() {
    _stopped = true;
    for (auto& stop : _stopTimers) {
        if (stop) stop();
    }
    _stopTimers.clear();
    if (_worker.joinable()) _worker.join();
}

void ServerStatusService::TriggerBackgroundUpdate() { RefreshAsync(false, "Background update"); }

std::vector<ServerCacheEntry> ServerStatusService::GetSnapshot() const {
    std::vector<ServerCacheEntry> result;
    if (!_servers) return result;

    std::lock_guard<std::mutex> guard(_cacheMutex);
    for (const ServerData& server : _servers->list) {
        const auto entry = _cache.find({server.ip, server.port});
        if (entry != _cache.end()) result.push_back(entry->second);
    }
    return result;
}

void ServerStatusService::RefreshAsync(bool force, const std::string& reason) {
    if (_stopped || !Enabled()) return;

    // Уже идёт проход — второй не нужен: !servers доступна любому игроку,
    // и без этого спам командой плодил бы параллельные опросы
    if (_inFlight.exchange(true)) {
        _logger->Debug("[ServerStatus] " + reason + " skipped: query already in flight");
        return;
    }

    // Прошлый поток уже закончил работу (флаг снимается последним), осталось его забрать
    if (_worker.joinable()) _worker.join();

    // Снимок — здесь, в главном потоке: в фон уходят только копии значений
    _worker = std::thread(&ServerStatusService::Run, this, _servers->list, TimeoutMs(), CacheTtlSeconds(), force,
                          reason);
}

void ServerStatusService::Run(std::vector<ServerData> servers, int timeoutMs, int ttlSeconds, bool force,
                              std::string reason) {
    const double now = _clock();

    std::vector<ServerData> toQuery;
    for (const ServerData& server : servers) {
        if (force || ttlSeconds == 0) {
            toQuery.push_back(server);
            continue;
        }
        std::lock_guard<std::mutex> guard(_cacheMutex);
        const auto entry = _cache.find({server.ip, server.port});
        if (entry == _cache.end() || now - entry->second.updatedAt >= ttlSeconds) toQuery.push_back(server);
    }

    if (toQuery.empty()) {
        BgDebug("[ServerStatus] " + reason + ": cache still fresh, nothing to query");
        _inFlight = false;
        return;
    }

    // Параллельно, но пачками: длинный список иначе поднял бы столько же сокетов разом.
    // Между пачками проверяем остановку — выгрузка ждёт не дольше одной пачки.
    for (size_t i = 0; i < toQuery.size() && !_stopped; i += kMaxConcurrentQueries) {
        std::vector<std::thread> batch;
        for (size_t k = i; k < toQuery.size() && k < i + kMaxConcurrentQueries; ++k) {
            batch.emplace_back(&ServerStatusService::QueryAndStore, this, std::cref(toQuery[k]), timeoutMs);
        }
        for (std::thread& worker : batch) worker.join();
    }

    int online = 0;
    int offline = 0;
    {
        std::lock_guard<std::mutex> guard(_cacheMutex);
        for (const auto& entry : _cache) (entry.second.online ? online : offline)++;
    }

    BgDebug("[ServerStatus] " + reason + " completed for " + std::to_string(toQuery.size()) + " server(s): " +
            std::to_string(online) + " online, " + std::to_string(offline) + " offline");
    _inFlight = false;
}

void ServerStatusService::QueryAndStore(const ServerData& server, int timeoutMs) {
    const std::optional<A2SInfo> info = _query(server.ip, static_cast<uint16_t>(server.port), timeoutMs);
    const auto lines = BuildServerLines(server, info);

    {
        std::lock_guard<std::mutex> guard(_cacheMutex);
        ServerCacheEntry& entry = _cache[{server.ip, server.port}];
        entry.chat = lines.first;
        entry.console = lines.second;
        entry.online = info.has_value();
        entry.updatedAt = _clock();
    }

    BgDebug("[ServerStatus] " + server.ip + ":" + std::to_string(server.port) + " - " +
            (info ? "ONLINE" : "OFFLINE"));
}

std::string ServerStatusService::SanitizeRemoteText(const std::string& value) {
    std::vector<uint32_t> kept;
    size_t units = 0;  // длина в UTF-16, как у string в C#

    for (const uint32_t cp : DecodeUtf8(value)) {
        if (units >= kMaxRemoteTextLength) break;
        if (IsControl(cp) || cp == '{' || cp == '}' || cp == 0x2029) continue;

        const size_t width = cp >= 0x10000 ? 2 : 1;
        if (units + width > kMaxRemoteTextLength) break;
        kept.push_back(cp);
        units += width;
    }

    size_t begin = 0;
    size_t end = kept.size();
    while (begin < end && IsWhiteSpace(kept[begin])) ++begin;
    while (end > begin && IsWhiteSpace(kept[end - 1])) --end;

    std::string out;
    for (size_t i = begin; i < end; ++i) AppendUtf8(kept[i], &out);
    return out;
}

std::pair<std::string, std::string> ServerStatusService::BuildServerLines(const ServerData& server,
                                                                          const std::optional<A2SInfo>& info) {
    std::string map = info ? SanitizeRemoteText(info->map) : std::string("OFFLINE");
    if (map.empty()) map = "?";

    const std::string players =
        info ? std::to_string(info->players > info->bots ? info->players - info->bots : 0) : std::string("0");
    const std::string max = info                         ? std::to_string(info->maxPlayers)
                            : server.maxPlayersFallback ? std::to_string(*server.maxPlayersFallback)
                                                         : std::string("?");

    const std::string templateText =
        !server.messageTemplate.empty()
            ? server.messageTemplate
            : std::string("{SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}");
    const std::string consoleTemplate =
        !server.messageTemplateConsole.empty() ? server.messageTemplateConsole : templateText;

    return {Fill(templateText, server, map, players, max), Fill(consoleTemplate, server, map, players, max)};
}

void ServerStatusService::AnnounceToPlayer(const std::string& playerName, const std::string& title,
                                           const std::function<void(MessageType, const std::string&)>& print) const {
    if (!Enabled()) {
        _logger->Debug("[ServerStatus] AnnounceToPlayer called but servers disabled or empty");
        return;
    }

    _logger->Debug("[ServerStatus] Showing server list to " + playerName);

    const std::vector<ServerCacheEntry> snapshot = GetSnapshot();
    _logger->Debug("[ServerStatus] Cache snapshot contains " + std::to_string(snapshot.size()) + " server(s)");

    if (snapshot.empty()) {
        _logger->Debug("[ServerStatus] Cache is empty, servers may not have been queried yet");
        return;
    }

    if (!title.empty()) print(MessageType::Chat, title);
    for (const ServerCacheEntry& entry : snapshot) {
        if (!entry.chat.empty()) print(MessageType::Chat, entry.chat);
    }
    for (const ServerCacheEntry& entry : snapshot) {
        if (!entry.console.empty()) print(MessageType::Console, entry.console);
    }

    _logger->Debug("[ServerStatus] Finished showing server list");
}

}  // namespace nm
