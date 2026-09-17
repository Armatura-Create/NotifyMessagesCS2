using System;
using System.Collections.Generic;

namespace NotifyMessages;

/// Сервис сессий игроков: таймеры подключения и набор fully-connected.
///
/// Таймер хранится как «как его остановить», а не как тип фреймворка: класс одинаков
/// в cssharp/ и swiftly/, где таймеры устроены по-разному.
public sealed class SessionService
{
    private readonly object _lock = new();
    private readonly Dictionary<ulong, Action> _connectionTimers = new();
    private readonly HashSet<ulong> _fullyConnectedPlayers = new();

    // Язык клиента (cl_language), снятый при подключении. Чистится в EventPlayerDisconnect
    // вместе с остальным состоянием игрока — иначе словарь растёт всё время жизни сервера.
    private readonly Dictionary<ulong, string> _languages = new();

    // Игроки, вернувшиеся на сервер после смены карты, и момент возврата.
    // В CS2 при смене карты player_disconnect не приходит, а player_connect_full приходит
    // заново: второй connect_full у уже fully-connected игрока — это возврат, а не заход.
    // Его первое попадание в команду на новой карте — тоже не событие, и метка живёт
    // до этого момента (или до истечения срока, чтобы не съесть настоящий переход позже).
    private readonly Dictionary<ulong, DateTime> _returning = new();

    public void SetConnectionTimer(ulong steamId, Action stopTimer)
    {
        lock (_lock)
        {
            if (_connectionTimers.TryGetValue(steamId, out var existing))
                existing();
            _connectionTimers[steamId] = stopTimer;
        }
    }

    public bool TryKillAndRemoveConnectionTimer(ulong steamId)
    {
        lock (_lock)
        {
            if (_connectionTimers.TryGetValue(steamId, out var stop))
            {
                stop();
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

    public void SetLanguage(ulong steamId, string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return;

        lock (_lock)
        {
            _languages[steamId] = language;
        }
    }

    public string? GetLanguage(ulong steamId)
    {
        lock (_lock)
        {
            return _languages.TryGetValue(steamId, out var lang) ? lang : null;
        }
    }

    public void RemoveLanguage(ulong steamId)
    {
        lock (_lock)
        {
            _languages.Remove(steamId);
        }
    }

    public void MarkReturning(ulong steamId, DateTime now)
    {
        lock (_lock)
        {
            _returning[steamId] = now;
        }
    }

    /// Снимает метку возврата и говорит, была ли она свежей. Метка снимается в любом
    /// случае: просроченная не должна ждать следующего перехода.
    public bool TakeReturning(ulong steamId, DateTime now, TimeSpan maxAge)
    {
        lock (_lock)
        {
            if (!_returning.Remove(steamId, out var markedAt)) return false;
            return now - markedAt <= maxAge;
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
            _returning.Remove(steamId);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (var kv in _connectionTimers) kv.Value();
            _connectionTimers.Clear();
            _fullyConnectedPlayers.Clear();
            _languages.Clear();
            _returning.Clear();
        }
    }
}
