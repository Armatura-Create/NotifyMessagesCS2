// Страна и город игрока по IP. Порт Services/GeoIpService.cs.
//
// Базы MaxMind открываются ТОЛЬКО из памяти (nm_mmdb_open_memory): штатный
// MMDB_open делает mmap, и страничный отказ внутри игрового процесса — это SIGBUS
// и смерть сервера без стека в логе. Цена — RAM размером с базу (Country ~8 МБ,
// City ~60 МБ). Базы открываются лениво, на первом игроке, и ровно один раз.
//
// Главный поток: и события, и таймеры этой цели живут в GameFrame.
#pragma once

#include <cstdint>
#include <map>
#include <string>
#include <utility>

namespace nm {

class ILogger;

class GeoIpService {
public:
    GeoIpService(std::string directory, ILogger* logger) : _directory(std::move(directory)), _logger(logger) {}
    ~GeoIpService();

    GeoIpService(const GeoIpService&) = delete;
    GeoIpService& operator=(const GeoIpService&) = delete;

    // Кеш на игрока: страна (или defaultLang, если база не ответила) и город
    void UpdatePlayerCache(uint64_t steamId, const std::string& ip, const std::string& defaultLang);
    bool TryGetPlayerIso(uint64_t steamId, std::string* iso) const;
    bool TryGetPlayerCity(uint64_t steamId, std::string* city) const;
    std::string GetIsoForSteamId(uint64_t steamId) const;  // пусто — не кеширован
    void RemovePlayer(uint64_t steamId);
    void ClearPlayers();

    // ISO-код страны; для приватного адреса и без базы — defaultLang
    std::string GetIsoCode(const std::string& ip, const std::string& defaultLang);
    std::string GetCity(const std::string& ip);

    void Close();

    // IP из "1.2.3.4:27015", "1.2.3.4" или "[::1]:27015". Наивный split по ':'
    // ломал любой IPv6-адрес.
    static std::string ExtractIp(const std::string& rawAddress);

    // Неразбираемая строка тоже считается локальной
    static bool IsLocalOrPrivate(const std::string& ip);

private:
    struct Database;
    Database* Open(Database** slot, const char* fileName);

    std::string _directory;
    ILogger* _logger;
    Database* _country = nullptr;
    Database* _city = nullptr;

    std::map<uint64_t, std::string> _playerIso;
    std::map<uint64_t, std::string> _playerCity;
};

}  // namespace nm
