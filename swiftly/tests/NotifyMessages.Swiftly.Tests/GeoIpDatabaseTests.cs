using System;
using System.IO;
using Xunit;

namespace NotifyMessages.Tests;

/// Реальное чтение закоммиченных баз MaxMind.
///
/// Регрессия: сервер умирал внутри конструктора DatabaseReader без исключения и без строки
/// в логе. Причина — дефолтный FileAccessMode.MemoryMapped: страничный отказ в отображённом
/// регионе внутри игрового процесса даёт SIGBUS, который убивает процесс мгновенно.
/// Тест проверяет обе вещи разом: база в репозитории читается, и режим доступа рабочий.
public class GeoIpDatabaseTests
{
    /// Каталог GeoIP ищем вверх от бинарника: тесты запускаются из bin/Debug/net10.0
    private static string FindGeoIpDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "GeoIP");
            if (File.Exists(Path.Combine(candidate, "GeoLite2-Country.mmdb"))) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("GeoIP/GeoLite2-Country.mmdb не найден выше " + AppContext.BaseDirectory);
    }

    [Fact]
    public void CountryDatabase_OpensAndResolvesKnownAddress()
    {
        var logger = new RecordingLogger();
        using var service = new GeoIpService(FindGeoIpDirectory(), logger);

        // 8.8.8.8 — Google DNS, стабильно числится за US во всех выпусках GeoLite2
        Assert.Equal("US", service.GetIsoCode("8.8.8.8", "RU"));
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public void PrivateAddress_DoesNotTouchDatabase()
    {
        var logger = new RecordingLogger();
        using var service = new GeoIpService(FindGeoIpDirectory(), logger);

        Assert.Equal("RU", service.GetIsoCode("192.168.1.10", "RU"));
    }
}
