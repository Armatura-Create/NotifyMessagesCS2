#include "core/players.h"

namespace nm {

void PlayerRegistry::Connect(int slot, uint64_t steamId, const std::string& name, const std::string& ip,
                             bool fake) {
    // Новое подключение в слот — новый игрок: всё, что знали о прежнем, забываем
    PlayerInfo info;
    info.slot = slot;
    info.steamId = steamId;
    info.name = name;
    info.ip = ip;
    info.fake = fake || steamId == 0;
    _slots[slot] = info;
}

void PlayerRegistry::PutInServer(int slot, uint64_t steamId, const std::string& name) {
    auto known = _slots.find(slot);
    if (known == _slots.end() || known->second.steamId != steamId) {
        // Слот без OnClientConnected — поздняя загрузка плагина или бот
        Connect(slot, steamId, name, std::string(), steamId == 0);
        known = _slots.find(slot);
    }
    if (!name.empty()) known->second.name = name;
    known->second.inGame = true;
}

void PlayerRegistry::SetName(int slot, const std::string& name) {
    const auto known = _slots.find(slot);
    if (known != _slots.end() && !name.empty()) known->second.name = name;
}

void PlayerRegistry::Disconnect(int slot) { _slots.erase(slot); }

const PlayerInfo* PlayerRegistry::Find(int slot) const {
    const auto known = _slots.find(slot);
    return known != _slots.end() ? &known->second : nullptr;
}

const PlayerInfo* PlayerRegistry::FindBySteamId(uint64_t steamId) const {
    if (steamId == 0) return nullptr;
    for (const auto& entry : _slots) {
        const PlayerInfo& info = entry.second;
        if (info.inGame && !info.fake && info.steamId == steamId) return &info;
    }
    return nullptr;
}

std::vector<PlayerInfo> PlayerRegistry::Humans() const {
    std::vector<PlayerInfo> humans;
    for (const auto& entry : _slots) {
        if (entry.second.inGame && !entry.second.fake) humans.push_back(entry.second);
    }
    return humans;
}

int PlayerRegistry::CountInGame() const {
    int count = 0;
    for (const auto& entry : _slots) count += entry.second.inGame ? 1 : 0;
    return count;
}

}  // namespace nm
