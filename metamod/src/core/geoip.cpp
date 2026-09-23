#include "core/geoip.h"

#include "core/geoip_memory.h"
#include "core/logger.h"

#include <cctype>
#include <sys/stat.h>

#ifdef _WIN32
#include <winsock2.h>
#include <ws2tcpip.h>
#else
#include <arpa/inet.h>
#include <netinet/in.h>
#include <sys/socket.h>
#endif

namespace nm {

struct GeoIpService::Database {
    MMDB_s handle{};
    bool open = false;
};

namespace {

// Строковое поле результата. Пусто — поля нет: у мелких стран и городов обычное дело.
std::string ReadString(MMDB_lookup_result_s* result, const char* first, const char* second,
                       const char* third) {
    MMDB_entry_data_s data;
    const int status = third == nullptr ? MMDB_get_value(&result->entry, &data, first, second, NULL)
                                        : MMDB_get_value(&result->entry, &data, first, second, third, NULL);
    if (status != MMDB_SUCCESS || !data.has_data || data.type != MMDB_DATA_TYPE_UTF8_STRING) {
        return std::string();
    }
    return std::string(data.utf8_string, data.data_size);
}

bool Lookup(MMDB_s* handle, const std::string& ip, MMDB_lookup_result_s* result) {
    int gai = 0;
    int mmdb = MMDB_SUCCESS;
    *result = MMDB_lookup_string(handle, ip.c_str(), &gai, &mmdb);
    return gai == 0 && mmdb == MMDB_SUCCESS && result->found_entry;
}

std::string Trim(const std::string& value) {
    size_t begin = 0;
    size_t end = value.size();
    while (begin < end && std::isspace(static_cast<unsigned char>(value[begin]))) ++begin;
    while (end > begin && std::isspace(static_cast<unsigned char>(value[end - 1]))) --end;
    return value.substr(begin, end - begin);
}

}  // namespace

GeoIpService::~GeoIpService() { Close(); }

void GeoIpService::Close() {
    Database** slots[] = {&_country, &_city};
    for (Database** slot : slots) {
        if (*slot == nullptr) continue;
        if ((*slot)->open) nm_mmdb_close_memory(&(*slot)->handle);
        delete *slot;
        *slot = nullptr;
    }
}

// Путь и РАЗМЕР в логе не для красоты: недокачанная база выглядит как обычная —
// до первого чтения. Трассировка [GEO] печатается ПЕРЕД опасной операцией:
// по последней строке видно, на каком шаге умер процесс.
GeoIpService::Database* GeoIpService::Open(Database** slot, const char* fileName) {
    if (*slot != nullptr) return (*slot)->open ? *slot : nullptr;

    // Неудачная попытка не повторяется: иначе каждый заход стучался бы в пустой файл
    Database* database = new Database();
    *slot = database;

    const std::string path = _directory.empty() ? std::string(fileName) : _directory + "/" + fileName;

    struct stat info;
    if (stat(path.c_str(), &info) != 0) {
        _logger->Debug(std::string("[GEO] база ") + fileName + " не найдена (" + path + ") — гео недоступно");
        return nullptr;
    }

    const std::string size = std::to_string(static_cast<long long>(info.st_size));
    _logger->Debug(std::string("[GEO] 2/5 открываю ") + fileName + ", размер " + size + " байт");

    const int status = nm_mmdb_open_memory(path.c_str(), &database->handle);
    if (status != MMDB_SUCCESS) {
        _logger->Error(std::string("[GEO] не удалось открыть ") + fileName + ": " + MMDB_strerror(status));
        return nullptr;
    }

    database->open = true;
    _logger->Debug(std::string("[GEO] 2/5 ") + fileName + " открыта (из памяти, " + size + " байт в RAM)");
    return database;
}

void GeoIpService::UpdatePlayerCache(uint64_t steamId, const std::string& ip, const std::string& defaultLang) {
    _logger->Debug("[GEO] 1/5 запрос для " + std::to_string(steamId) + ", ip=" + ip);

    const std::string iso = GetIsoCode(ip, defaultLang);
    _logger->Debug("[GEO] 4/5 страна=" + iso + ", запрашиваю город");

    const std::string city = GetCity(ip);
    _logger->Debug("[GEO] 5/5 город=" + (city.empty() ? std::string("неизвестен") : city) + ", гео закешировано");

    _playerIso[steamId] = iso;
    _playerCity[steamId] = city;
}

bool GeoIpService::TryGetPlayerIso(uint64_t steamId, std::string* iso) const {
    const auto found = _playerIso.find(steamId);
    if (found == _playerIso.end()) return false;
    *iso = found->second;
    return true;
}

bool GeoIpService::TryGetPlayerCity(uint64_t steamId, std::string* city) const {
    const auto found = _playerCity.find(steamId);
    if (found == _playerCity.end()) return false;
    *city = found->second;
    return true;
}

std::string GeoIpService::GetIsoForSteamId(uint64_t steamId) const {
    std::string iso;
    return TryGetPlayerIso(steamId, &iso) ? iso : std::string();
}

void GeoIpService::RemovePlayer(uint64_t steamId) {
    _playerIso.erase(steamId);
    _playerCity.erase(steamId);
}

void GeoIpService::ClearPlayers() {
    _playerIso.clear();
    _playerCity.clear();
}

std::string GeoIpService::GetIsoCode(const std::string& ip, const std::string& defaultLang) {
    if (Trim(ip).empty() || IsLocalOrPrivate(ip)) return defaultLang;

    Database* country = Open(&_country, "GeoLite2-Country.mmdb");
    if (country == nullptr) return defaultLang;

    _logger->Debug("[GEO] 3/5 читаю страну из GeoLite2-Country.mmdb");
    MMDB_lookup_result_s result;
    if (!Lookup(&country->handle, ip, &result)) return defaultLang;

    const std::string iso = ReadString(&result, "country", "iso_code", nullptr);
    return iso.empty() ? defaultLang : iso;
}

std::string GeoIpService::GetCity(const std::string& ip) {
    if (Trim(ip).empty() || IsLocalOrPrivate(ip)) return std::string();

    Database* city = Open(&_city, "GeoLite2-City.mmdb");
    if (city == nullptr) return std::string();

    _logger->Debug("[GEO] читаю город из GeoLite2-City.mmdb");
    MMDB_lookup_result_s result;
    if (!Lookup(&city->handle, ip, &result)) return std::string();

    return ReadString(&result, "city", "names", "en");
}

std::string GeoIpService::ExtractIp(const std::string& rawAddress) {
    const std::string value = Trim(rawAddress);
    if (value.empty()) return std::string();

    // [ipv6]:port
    if (value[0] == '[') {
        const size_t close = value.find(']');
        if (close != std::string::npos && close > 1) return value.substr(1, close - 1);
        size_t start = 0;
        while (start < value.size() && value[start] == '[') ++start;
        return value.substr(start);
    }

    const size_t lastColon = value.rfind(':');
    if (lastColon == std::string::npos) return value;

    // Несколько двоеточий без скобок — голый IPv6, порта там нет
    if (value.find(':') != lastColon) return value;

    return value.substr(0, lastColon);
}

bool GeoIpService::IsLocalOrPrivate(const std::string& ip) {
    unsigned char v4[4];
    if (inet_pton(AF_INET, ip.c_str(), v4) == 1) {
        return v4[0] == 127 ||                                   // loopback
               v4[0] == 10 ||                                    // 10.0.0.0/8
               (v4[0] == 172 && v4[1] >= 16 && v4[1] <= 31) ||   // 172.16.0.0/12
               (v4[0] == 192 && v4[1] == 168) ||                 // 192.168.0.0/16
               (v4[0] == 169 && v4[1] == 254);                   // link-local
    }

    unsigned char v6[16];
    if (inet_pton(AF_INET6, ip.c_str(), v6) == 1) {
        bool loopback = v6[15] == 1;
        for (int i = 0; i < 15 && loopback; ++i) loopback = v6[i] == 0;
        if (loopback) return true;                                   // ::1
        if (v6[0] == 0xFE && (v6[1] & 0xC0) == 0x80) return true;    // fe80::/10 link-local
        if (v6[0] == 0xFE && (v6[1] & 0xC0) == 0xC0) return true;    // fec0::/10 site-local
        return (v6[0] & 0xFE) == 0xFC;                               // fc00::/7 unique local
    }

    return true;
}

}  // namespace nm
