using System;
using System.Collections.Concurrent;
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

    // Per-player geo cache moved from NotifyMessages.
    // Concurrent: к кешу обращаются и игровые события, и колбэки таймеров.
    private readonly ConcurrentDictionary<ulong, string> _playerIsoCode = new();
    private readonly ConcurrentDictionary<ulong, string> _playerCity = new();

    public GeoIpService(string moduleDirectory, ILogger logger)
    {
        _moduleDirectory = moduleDirectory;
        _logger = logger;
    }

    // Update per-player cache for a given SteamID and IP
    public void UpdatePlayerCache(ulong steamId, string ip, string defaultLang)
    {
        try
        {
            // Трассировка: базы MaxMind открываются лениво, ровно на первом игроке, и читаются
            // через memory-mapped файл. Недокачанная или битая .mmdb убивает процесс без
            // исключения — по последней напечатанной строке видно, на каком шаге это случилось.
            _logger.Debug($"[GEO] 1/5 запрос для {steamId}, ip={ip}");

            var iso = GetIsoCode(ip, defaultLang);
            _logger.Debug($"[GEO] 4/5 страна={iso}, запрашиваю город");

            var city = GetCity(ip);
            _logger.Debug($"[GEO] 5/5 город={(string.IsNullOrEmpty(city) ? "неизвестен" : city)}, гео закешировано");

            _playerIsoCode[steamId] = iso;
            _playerCity[steamId] = city;
        }
        catch (Exception ex)
        {
            _logger.Error($"UpdatePlayerCache failed for {steamId}", ex);
        }
    }

    public bool TryGetPlayerIso(ulong steamId, out string iso)
    {
        return _playerIsoCode.TryGetValue(steamId, out iso!);
    }

    public bool TryGetPlayerCity(ulong steamId, out string city)
    {
        return _playerCity.TryGetValue(steamId, out city!);
    }

    public string? GetIsoForSteamId(ulong steamId)
    {
        return _playerIsoCode.TryGetValue(steamId, out var iso) ? iso : null;
    }

    public void RemovePlayer(ulong steamId)
    {
        _playerIsoCode.TryRemove(steamId, out _);
        _playerCity.TryRemove(steamId, out _);
    }

    /// Достаёт IP из строки вида "1.2.3.4:27015", "1.2.3.4" или "[::1]:27015".
    /// Наивный Split(':')[0] ломал любой IPv6-адрес.
    public static string ExtractIp(string? rawAddress)
    {
        if (string.IsNullOrWhiteSpace(rawAddress)) return string.Empty;

        var value = rawAddress.Trim();

        // [ipv6]:port
        if (value.StartsWith('['))
        {
            var close = value.IndexOf(']');
            return close > 1 ? value.Substring(1, close - 1) : value.TrimStart('[');
        }

        var lastColon = value.LastIndexOf(':');
        if (lastColon < 0) return value;

        // Несколько двоеточий без скобок - это голый IPv6, порта там нет
        if (value.IndexOf(':') != lastColon)
            return value;

        return value.Substring(0, lastColon);
    }

    public void ClearPlayers()
    {
        _playerIsoCode.Clear();
        _playerCity.Clear();
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

            _logger.Debug("[GEO] 3/5 читаю страну из GeoLite2-Country.mmdb");
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

            _logger.Debug("[GEO] читаю город из GeoLite2-City.mmdb");
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
                _countryDbReader = OpenDatabase("GeoLite2-Country.mmdb");
        }
    }

    private void EnsureCityReader()
    {
        if (_cityDbReader != null) return;
        lock (_lock)
        {
            if (_cityDbReader == null)
                _cityDbReader = OpenDatabase("GeoLite2-City.mmdb");
        }
    }

    /// Открывает базу MaxMind, печатая путь и РАЗМЕР файла.
    ///
    /// Размер в логе не для красоты: базы качаются на этапе сборки, и недокачанный файл
    /// выглядит как обычный — до первого чтения. DatabaseReader работает через
    /// memory-mapped файл, поэтому битая база роняет процесс без исключения.
    /// Ориентиры: Country ~9 МБ, City ~60 МБ.
    private DatabaseReader? OpenDatabase(string fileName)
    {
        var path = System.IO.Path.Combine(_moduleDirectory, fileName);

        if (!System.IO.File.Exists(path))
        {
            _logger.Debug($"[GEO] база {fileName} не найдена ({path}) — гео недоступно");
            return null;
        }

        var size = new System.IO.FileInfo(path).Length;
        _logger.Debug($"[GEO] 2/5 открываю {fileName}, размер {size} байт");

        var reader = new DatabaseReader(path);
        _logger.Debug($"[GEO] 2/5 {fileName} открыта");

        return reader;
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
