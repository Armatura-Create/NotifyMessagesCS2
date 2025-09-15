using System.Collections.Generic;
using CounterStrikeSharp.API.Modules.Timers;

namespace NotifyMessages;

/// Сервис сессий игроков: таймеры подключения и набор fully-connected
public sealed class SessionService
{
    private readonly Dictionary<ulong, Timer> _connectionTimers = new();
    private readonly HashSet<ulong> _fullyConnectedPlayers = new();

    public bool TryGetConnectionTimer(ulong steamId, out Timer timer) => _connectionTimers.TryGetValue(steamId, out timer!);

    public void SetConnectionTimer(ulong steamId, Timer timer)
    {
        if (_connectionTimers.TryGetValue(steamId, out var existing))
            existing.Kill();
        _connectionTimers[steamId] = timer;
    }

    public bool TryKillAndRemoveConnectionTimer(ulong steamId)
    {
        if (_connectionTimers.TryGetValue(steamId, out var t))
        {
            t.Kill();
            _connectionTimers.Remove(steamId);
            return true;
        }
        return false;
    }

    public void RemoveConnectionTimer(ulong steamId)
    {
        _connectionTimers.Remove(steamId);
    }

    public void AddFullyConnected(ulong steamId) => _fullyConnectedPlayers.Add(steamId);
    public bool IsFullyConnected(ulong steamId) => _fullyConnectedPlayers.Contains(steamId);
    public void RemoveFullyConnected(ulong steamId) => _fullyConnectedPlayers.Remove(steamId);

    public void Clear()
    {
        foreach (var kv in _connectionTimers) kv.Value.Kill();
        _connectionTimers.Clear();
        _fullyConnectedPlayers.Clear();
    }
}
