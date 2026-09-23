// A2S_INFO: challenge, split-пакеты, таймаут. Порт AdvancedA2S.cs.
//
// Всё, что приходит по сети, — недоверенный ввод: каждое чтение проверяет границы
// буфера, ответы принимаются только с адреса опрашиваемого сервера, строки
// остаются сырыми байтами UTF-8 — чистит их ServerStatusService::SanitizeRemoteText.
//
// QueryInfo блокирует поток на время таймаута — вызывать ТОЛЬКО из фонового потока.
#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

namespace nm {

struct A2SInfo {
    uint8_t protocol = 0;
    std::string serverName;
    std::string map;
    std::string gameDir;
    std::string gameDesc;
    int16_t appId = 0;
    uint8_t players = 0;
    uint8_t maxPlayers = 0;
    uint8_t bots = 0;
};

namespace a2s {

// Разбор тела ответа. nullopt на любом усечённом или чужом пакете.
std::optional<A2SInfo> ParseInfo(const std::vector<uint8_t>& raw);

// Строка до '\0'. false — терминатора нет, то есть пакет усечён.
bool ReadNullTerminated(const std::vector<uint8_t>& data, size_t* index, std::string* out);

// Сетевой опрос. nullopt — сервер не ответил, ответил мусором или не резолвится.
std::optional<A2SInfo> QueryInfo(const std::string& host, uint16_t port, int timeoutMs);

}  // namespace a2s
}  // namespace nm
