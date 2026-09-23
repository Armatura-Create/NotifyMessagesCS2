// Состояние игроков между событиями: fully-connected, язык клиента, метка
// возврата после смены карты, таймер анонса входа. Порт Services/SessionService.cs.
//
// Всё ключуется по SteamID, никогда по слоту: слот движок переиспользует.
// Замков нет сознательно: в этой цели и события, и таймеры, и команды идут
// из главного потока (GameFrame), а фоновый опрос A2S сюда не ходит.
#pragma once

#include <cstdint>
#include <functional>
#include <map>
#include <set>
#include <string>
#include <utility>
#include <vector>

namespace nm {

class SessionService {
public:
    // stopTimer — «как остановить» таймер: сервис не знает, как устроен планировщик
    void SetConnectionTimer(uint64_t steamId, std::function<void()> stopTimer);
    bool TryKillAndRemoveConnectionTimer(uint64_t steamId);
    void RemoveConnectionTimer(uint64_t steamId);

    void SetLanguage(uint64_t steamId, const std::string& language);
    std::string GetLanguage(uint64_t steamId) const;  // пусто — неизвестен
    void RemoveLanguage(uint64_t steamId);

    // При смене карты player_disconnect не приходит, а connect_full приходит
    // заново: второй connect_full у fully-connected игрока — возврат, а не заход.
    // Метка живёт до первого попадания в команду или до истечения срока.
    void MarkReturning(uint64_t steamId, double now);
    // Снимает метку и говорит, была ли она свежей. Снимается в любом случае:
    // просроченная не должна ждать следующего перехода.
    bool TakeReturning(uint64_t steamId, double now, double maxAgeSeconds);

    void AddFullyConnected(uint64_t steamId);
    bool IsFullyConnected(uint64_t steamId) const;
    void RemoveFullyConnected(uint64_t steamId);

    void Clear();

private:
    std::map<uint64_t, std::function<void()>> _connectionTimers;
    std::set<uint64_t> _fullyConnected;
    std::map<uint64_t, std::string> _languages;
    std::map<uint64_t, double> _returning;
};

// Отличает смену сторон (halftime, mp_swapteams) от перехода одного игрока:
// движок меняет стороны всем синхронно, в одном кадре. Пачка из двух и более
// T<->CT за кадр — смена сторон. Вторая линия обороны рядом с player_team.silent.
namespace team_swap {

bool IsSideSwitch(int oldTeam, int newTeam);
bool IsMassSwap(const std::vector<std::pair<int, int>>& changes);

}  // namespace team_swap
}  // namespace nm
