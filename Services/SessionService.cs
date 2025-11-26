using System.Collections.Generic;
using CounterStrikeSharp.API.Modules.Timers;

namespace NotifyMessages;

/// Сервис сессий игроков: таймеры подключения и набор fully-connected
public sealed class SessionService
{
    private readonly object _lock = new();
    private readonly Dictionary<ulong, Timer> _connectionTimers = new();
    private readonly HashSet<ulong> _fullyConnectedPlayers = new();

    public bool TryGetConnectionTimer(ulong steamId, out Timer timer)
    {
        lock (_lock)
        {
            return _connectionTimers.TryGetValue(steamId, out timer!);
        }
    }

    public void SetConnectionTimer(ulong steamId, Timer timer)
    {
        lock (_lock)
        {
            if (_connectionTimers.TryGetValue(steamId, out var existing))
                existing.Kill();
            _connectionTimers[steamId] = timer;
        }
    }

    public bool TryKillAndRemoveConnectionTimer(ulong steamId)
    {
        lock (_lock)
        {
            if (_connectionTimers.TryGetValue(steamId, out var t))
            {
                t.Kill();
                _connectionTimers.Remove(steamId);
                return true;
            }
            return false;
        }
    }

    public void RemoveConnectionTimer(ulong steamId)
    {
        lock (_lock)
        {
            _connectionTimers.Remove(steamId);
        }
    }

    public void AddFullyConnected(ulong steamId)
    {
        lock (_lock)
        {
            _fullyConnectedPlayers.Add(steamId);
        }
    }

    public bool IsFullyConnected(ulong steamId)
    {
        lock (_lock)
        {
            return _fullyConnectedPlayers.Contains(steamId);
        }
    }

    public void RemoveFullyConnected(ulong steamId)
    {
        lock (_lock)
        {
            _fullyConnectedPlayers.Remove(steamId);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (var kv in _connectionTimers) kv.Value.Kill();
            _connectionTimers.Clear();
            _fullyConnectedPlayers.Clear();
        }
    }
}
