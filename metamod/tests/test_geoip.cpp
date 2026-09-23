// Реальное чтение закоммиченных баз MaxMind ИЗ ПАМЯТИ. Порт GeoIpDatabaseTests.cs
// и GeoIpAddressTests.cs.
//
// Регрессия, ради которой всё это: сервер умирал на открытии базы без исключения и
// без строки в логе — страничный отказ в mmap-регионе внутри игрового процесса даёт
// SIGBUS. Тест проверяет обе вещи: база из репозитория читается, и режим рабочий.
#include "core/geoip.h"
#include "helpers.h"

#include "doctest.h"

#include <sys/stat.h>

using namespace nm;

namespace {

// Тесты запускаются из metamod/, базы — одна копия на все цели
const char* kGeoDirectory = "../GeoIP";

bool DatabasesPresent() {
    struct stat info;
    return stat((std::string(kGeoDirectory) + "/GeoLite2-Country.mmdb").c_str(), &info) == 0;
}

}  // namespace

TEST_CASE("Страна по известному адресу") {
    REQUIRE_MESSAGE(DatabasesPresent(), "GeoIP/GeoLite2-Country.mmdb не найден — тесты запускаются из metamod/");

    nm_test::RecordingLogger logger;
    GeoIpService geo(kGeoDirectory, &logger);

    // 8.8.8.8 — Google DNS, стабильно числится за US во всех выпусках GeoLite2
    CHECK(geo.GetIsoCode("8.8.8.8", "RU") == "US");
    CHECK(logger.errors.empty());
}

TEST_CASE("Кеш игрока: страна и город, чистка при выходе") {
    REQUIRE(DatabasesPresent());

    nm_test::RecordingLogger logger;
    GeoIpService geo(kGeoDirectory, &logger);

    geo.UpdatePlayerCache(1, "8.8.8.8", "RU");
    std::string iso;
    std::string city;
    CHECK(geo.TryGetPlayerIso(1, &iso));
    CHECK(iso == "US");
    CHECK(geo.TryGetPlayerCity(1, &city));  // города у anycast-адреса может не быть — сам факт кеша
    CHECK(geo.GetIsoForSteamId(1) == "US");

    geo.RemovePlayer(1);
    CHECK_FALSE(geo.TryGetPlayerIso(1, &iso));
    CHECK(geo.GetIsoForSteamId(1).empty());
}

TEST_CASE("Приватный адрес не трогает базу и даёт язык по умолчанию") {
    nm_test::RecordingLogger logger;
    GeoIpService geo("/nonexistent", &logger);
    CHECK(geo.GetIsoCode("192.168.1.10", "RU") == "RU");
    CHECK(geo.GetCity("10.0.0.1").empty());
    CHECK(logger.debugs.empty());
}

TEST_CASE("Нет базы — значение по умолчанию, без ошибки") {
    nm_test::RecordingLogger logger;
    GeoIpService geo("/nonexistent", &logger);
    CHECK(geo.GetIsoCode("8.8.8.8", "RU") == "RU");
    CHECK(logger.errors.empty());
}

TEST_CASE("IP из адреса клиента: IPv4 и IPv6") {
    CHECK(GeoIpService::ExtractIp("1.2.3.4:27015") == "1.2.3.4");
    CHECK(GeoIpService::ExtractIp("1.2.3.4") == "1.2.3.4");
    CHECK(GeoIpService::ExtractIp("[2001:db8::1]:27015") == "2001:db8::1");
    // Голый IPv6 — раньше ломался о Split(':')[0]
    CHECK(GeoIpService::ExtractIp("2001:db8::1") == "2001:db8::1");
    CHECK(GeoIpService::ExtractIp("::1") == "::1");
    CHECK(GeoIpService::ExtractIp("").empty());
}

TEST_CASE("Локальные и приватные адреса") {
    CHECK(GeoIpService::IsLocalOrPrivate("127.0.0.1"));
    CHECK(GeoIpService::IsLocalOrPrivate("10.1.2.3"));
    CHECK(GeoIpService::IsLocalOrPrivate("172.16.0.1"));
    CHECK(GeoIpService::IsLocalOrPrivate("172.31.255.255"));
    CHECK_FALSE(GeoIpService::IsLocalOrPrivate("172.32.0.1"));
    CHECK(GeoIpService::IsLocalOrPrivate("192.168.0.1"));
    CHECK(GeoIpService::IsLocalOrPrivate("169.254.1.1"));
    CHECK(GeoIpService::IsLocalOrPrivate("::1"));
    CHECK(GeoIpService::IsLocalOrPrivate("fe80::1"));
    CHECK(GeoIpService::IsLocalOrPrivate("fd00::1"));
    CHECK(GeoIpService::IsLocalOrPrivate("not an ip"));
    CHECK_FALSE(GeoIpService::IsLocalOrPrivate("8.8.8.8"));
    CHECK_FALSE(GeoIpService::IsLocalOrPrivate("2001:4860:4860::8888"));
}
