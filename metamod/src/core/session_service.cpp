#include "core/session_service.h"

#include <utility>
#include <vector>

namespace nm {

void SessionService::SetConnectionTimer(uint64_t steamId, std::function<void()> stopTimer) {
    const auto existing = _connectionTimers.find(steamId);
    if (existing != _connectionTimers.end() && existing->second) existing->second();
    _connectionTimers[steamId] = std::move(stopTimer);
}

bool SessionService::TryKillAndRemoveConnectionTimer(uint64_t steamId) {
    const auto timer = _connectionTimers.find(steamId);
    if (timer == _connectionTimers.end()) return false;

    // Сначала убрать, потом звать: остановка не должна видеть себя в словаре
    std::function<void()> stop = std::move(timer->second);
    _connectionTimers.erase(timer);
    if (stop) stop();
    return true;
}

void SessionService::RemoveConnectionTimer(uint64_t steamId) { _connectionTimers.erase(steamId); }

void SessionService::SetLanguage(uint64_t steamId, const std::string& language) {
    bool blank = true;
    for (const char c : language) blank = blank && (c == ' ' || c == '\t' || c == '\r' || c == '\n');
    if (!blank) _languages[steamId] = language;
}

std::string SessionService::GetLanguage(uint64_t steamId) const {
    const auto language = _languages.find(steamId);
    return language != _languages.end() ? language->second : std::string();
}

void SessionService::RemoveLanguage(uint64_t steamId) { _languages.erase(steamId); }

void SessionService::MarkReturning(uint64_t steamId, double now) { _returning[steamId] = now; }

bool SessionService::TakeReturning(uint64_t steamId, double now, double maxAgeSeconds) {
    const auto mark = _returning.find(steamId);
    if (mark == _returning.end()) return false;

    const double markedAt = mark->second;
    _returning.erase(mark);
    return now - markedAt <= maxAgeSeconds;
}

void SessionService::AddFullyConnected(uint64_t steamId) { _fullyConnected.insert(steamId); }

bool SessionService::IsFullyConnected(uint64_t steamId) const { return _fullyConnected.count(steamId) != 0; }

void SessionService::RemoveFullyConnected(uint64_t steamId) {
    _fullyConnected.erase(steamId);
    _returning.erase(steamId);
}

void SessionService::Clear() {
    std::map<uint64_t, std::function<void()>> timers;
    timers.swap(_connectionTimers);
    for (auto& timer : timers) {
        if (timer.second) timer.second();
    }
    _fullyConnected.clear();
    _languages.clear();
    _returning.clear();
}

namespace team_swap {

constexpr int kTerrorists = 2;
constexpr int kCounterTerrorists = 3;

bool IsSideSwitch(int oldTeam, int newTeam) {
    return (oldTeam == kTerrorists && newTeam == kCounterTerrorists) ||
           (oldTeam == kCounterTerrorists && newTeam == kTerrorists);
}

bool IsMassSwap(const std::vector<std::pair<int, int>>& changes) {
    if (changes.size() < 2) return false;
    for (const auto& change : changes) {
        if (!IsSideSwitch(change.first, change.second)) return false;
    }
    return true;
}

}  // namespace team_swap
}  // namespace nm
