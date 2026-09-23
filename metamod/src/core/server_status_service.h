// Статусы чужих серверов для !servers: опрос A2S, кеш и таймеры.
// Порт Services/ServerStatusService.cs.
//
// Опрос идёт в фоновом потоке и НЕ трогает движок — только UDP, строки и кеш
// под mutex. Логи и всё, что нужно главному потоку, уходят через runOnMainThread.
// Снимок конфига для фона делается в главном потоке: в фон уходят только копии.
#pragma once

#include "core/a2s.h"
#include "core/advertisement_service.h"
#include "core/config.h"

#include <atomic>
#include <functional>
#include <map>
#include <mutex>
#include <optional>
#include <string>
#include <thread>
#include <utility>
#include <vector>

namespace nm {

class ILogger;

struct ServerCacheEntry {
    std::string chat;
    std::string console;
    bool online = false;
    double updatedAt = 0;  // секунды монотонных часов
};

class ServerStatusService {
public:
    // Потокобезопасна: выполнить действие в главном потоке (Scheduler::NextFrame)
    using RunOnMainThread = std::function<void(std::function<void()>)>;
    using QueryFunction = std::function<std::optional<A2SInfo>(const std::string&, uint16_t, int)>;
    using Clock = std::function<double()>;

    // query подменяют тесты; в игре это a2s::QueryInfo
    ServerStatusService(const std::optional<ServersConfig>& servers, ILogger* logger, RepeatEvery repeatEvery,
                        RunOnMainThread runOnMainThread, Clock clock, QueryFunction query = a2s::QueryInfo);

    // Ждёт фоновый проход: поток живёт в коде этой библиотеки, и выгрузка плагина
    // под работающим потоком — выполнение уже освобождённой памяти.
    ~ServerStatusService();

    ServerStatusService(const ServerStatusService&) = delete;
    ServerStatusService& operator=(const ServerStatusService&) = delete;

    void InitialQuery();
    void Start();
    // Останавливает таймер и ждёт текущую пачку запросов (не дольше таймаута опроса)
    void Stop();

    // Обновить кеш в фоне к следующему запросу (уважает TTL и in-flight guard)
    void TriggerBackgroundUpdate();

    // КОПИЯ кеша в порядке Servers.json: наружу голый словарь не отдаётся
    std::vector<ServerCacheEntry> GetSnapshot() const;

    // Список серверов игроку: print уже привязан к получателю.
    // Локализацию и рендер делает DisplayService::Print.
    void AnnounceToPlayer(const std::string& playerName, const std::string& title,
                          const std::function<void(MessageType, const std::string&)>& print) const;

    // Имя карты от чужого сервера пишет чужой админ: скобки, управляющие символы
    // и U+2029 вырезаются, длина ≤ 64, невалидный UTF-8 — U+FFFD.
    static std::string SanitizeRemoteText(const std::string& value);

    static std::pair<std::string, std::string> BuildServerLines(const ServerData& server,
                                                                const std::optional<A2SInfo>& info);

    int TimeoutMs() const;
    int CacheTtlSeconds() const;

private:
    bool Enabled() const;
    void RefreshAsync(bool force, const std::string& reason);
    void Run(std::vector<ServerData> servers, int timeoutMs, int ttlSeconds, bool force, std::string reason);
    void QueryAndStore(const ServerData& server, int timeoutMs);

    void BgDebug(const std::string& message);

    std::optional<ServersConfig> _servers;
    ILogger* _logger;
    RepeatEvery _repeatEvery;
    RunOnMainThread _runOnMainThread;
    Clock _clock;
    QueryFunction _query;

    mutable std::mutex _cacheMutex;
    std::map<std::pair<std::string, int>, ServerCacheEntry> _cache;

    std::vector<std::function<void()>> _stopTimers;
    std::thread _worker;
    std::atomic<bool> _inFlight{false};
    std::atomic<bool> _stopped{false};
};

}  // namespace nm
