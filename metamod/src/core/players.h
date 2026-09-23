// Кто сейчас на сервере — по данным хуков IServerGameClients.
//
// В C#-целях это Utilities.GetPlayers() / Core.PlayerManager. Здесь сущностей
// движка не читаем вовсе: SteamID, ник и IP отдаёт сам движок в OnClientConnected,
// а в игре игрок с ClientPutInServer. Поэтому ни одного смещения и ни одного
// обращения к контроллеру — а значит и того класса падений, из-за которого в
// cssharp/ запрещён GetPlayerFromSlot.
//
// Слот — только адрес доставки в пределах кадра. Через таймер передаётся
// SteamID, а слот ищется заново (FindBySteamId): за задержку игрок успевает выйти,
// и его слот достаётся другому.
#pragma once

#include <cstdint>
#include <map>
#include <string>
#include <vector>

namespace nm {

struct PlayerInfo {
    int slot = -1;
    uint64_t steamId = 0;
    std::string name;
    std::string ip;
    bool fake = false;    // бот
    bool inGame = false;  // после ClientPutInServer
};

class PlayerRegistry {
public:
    void Connect(int slot, uint64_t steamId, const std::string& name, const std::string& ip, bool fake);
    void PutInServer(int slot, uint64_t steamId, const std::string& name);
    void SetName(int slot, const std::string& name);
    void Disconnect(int slot);
    void Clear() { _slots.clear(); }

    const PlayerInfo* Find(int slot) const;
    // Живой человек с таким SteamID, уже в игре
    const PlayerInfo* FindBySteamId(uint64_t steamId) const;

    // Люди в игре — получатели рассылок. Копии: список не должен меняться под
    // ногами у цикла рассылки, если игрок выйдет посреди неё.
    std::vector<PlayerInfo> Humans() const;

    // Все в игре, включая ботов, — для {PLAYERS}
    int CountInGame() const;

private:
    std::map<int, PlayerInfo> _slots;
};

}  // namespace nm
