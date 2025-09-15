using System;
using System.Net;
using MaxMind.GeoIP2;

namespace NotifyMessages;

/// Сервис GeoIP: потокобезопасный доступ к базам MaxMind и кеширование ридеров
public sealed class GeoIpService : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _moduleDirectory;

    private readonly object _lock = new();
    private DatabaseReader? _countryDbReader;
    private DatabaseReader? _cityDbReader;

    public GeoIpService(string moduleDirectory, ILogger logger)
    {
        _moduleDirectory = moduleDirectory;
        _logger = logger;
    }

    /// Возвращает ISO‑код страны по IP. Если база недоступна — возвращает defaultLang
    public string GetIsoCode(string ip, string defaultLang)
    {
        if (string.IsNullOrWhiteSpace(ip)) return defaultLang;
        if (IsLocalOrPrivate(ip)) return defaultLang;
        try
        {
            EnsureCountryReader();
            if (_countryDbReader == null)
                return defaultLang;

            if (!IPAddress.TryParse(ip, out var ipAddr))
                return defaultLang;

            var response = _countryDbReader.Country(ipAddr);
            return response.Country.IsoCode ?? defaultLang;
        }
        catch (Exception ex)
        {
            _logger.Error("Country lookup error", ex);
            return defaultLang;
        }
    }

    /// Возвращает название города по IP (или пустую строку)
    public string GetCity(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return string.Empty;
        if (IsLocalOrPrivate(ip)) return string.Empty;
        try
        {
            EnsureCityReader();
            if (_cityDbReader == null)
                return string.Empty;

            if (!IPAddress.TryParse(ip, out var ipAddr))
                return string.Empty;

            var response = _cityDbReader.City(ipAddr);
            return response.City?.Name ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.Error("City lookup error", ex);
            return string.Empty;
        }
    }

    private void EnsureCountryReader()
    {
        if (_countryDbReader != null) return;
        lock (_lock)
        {
            if (_countryDbReader == null)
            {
                var path = System.IO.Path.Combine(_moduleDirectory, "GeoLite2-Country.mmdb");
                _countryDbReader = System.IO.File.Exists(path) ? new DatabaseReader(path) : null;
            }
        }
    }

    private void EnsureCityReader()
    {
        if (_cityDbReader != null) return;
        lock (_lock)
        {
            if (_cityDbReader == null)
            {
                var path = System.IO.Path.Combine(_moduleDirectory, "GeoLite2-City.mmdb");
                _cityDbReader = System.IO.File.Exists(path) ? new DatabaseReader(path) : null;
            }
        }
    }

    private static bool IsLocalOrPrivate(string ip)
    {
        if (!IPAddress.TryParse(ip, out var addr)) return true;
        if (IPAddress.IsLoopback(addr)) return true;

        if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = addr.GetAddressBytes();
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 169.254.0.0/16 (link-local)
            if (bytes[0] == 169 && bytes[1] == 254) return true;
        }
        else if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (addr.IsIPv6LinkLocal || addr.IsIPv6SiteLocal) return true;
            // Unique local addresses fc00::/7
            var bytes = addr.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC) return true;
        }

        return false;
    }

    public void Dispose()
    {
        try { _countryDbReader?.Dispose(); } catch { /* ignore */ }
        try { _cityDbReader?.Dispose(); } catch { /* ignore */ }
        _countryDbReader = null;
        _cityDbReader = null;
    }
}
