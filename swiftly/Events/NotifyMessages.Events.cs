using System;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;

namespace NotifyMessages;

/// Единственная точка регистрации обработчиков.
///
/// Каждый обработчик обёрнут в SafeEvent: исключение в нашем коде не должно всплывать
/// во фреймворк и мешать другим плагинам на сервере.
///
/// Хуки снимаются в Unload по сохранённым Guid: иначе после hot reload обработчик
/// отработает дважды.
///
/// OnTick здесь нет сознательно: HTML-панель держит на экране сам SwiftlyS2
/// (SendCenterHTML с длительностью), перерисовка тиками не нужна.
public sealed partial class NotifyMessages
{
    private Guid? _hookConnectFull;
    private Guid? _hookDisconnectPre;
    private Guid? _hookDisconnect;
    private Guid? _hookTeamPre;
    private Guid? _hookTeam;

    private void RegisterEvents()
    {
        _hookConnectFull = Core.GameEvent.HookPost<EventPlayerConnectFull>(
            SafeEvent<EventPlayerConnectFull>(OnPlayerConnectFull));

        // Pre-хуки гасят штатные сообщения движка «X покинул игру» / «X перешёл в команду»:
        // плагин показывает свои, на языке каждого игрока.
        _hookDisconnectPre = Core.GameEvent.HookPre<EventPlayerDisconnect>(
            SafeEvent<EventPlayerDisconnect>(ev =>
            {
                ev.DontBroadcast = true;
                return HookResult.Continue;
            }));

        _hookDisconnect = Core.GameEvent.HookPost<EventPlayerDisconnect>(
            SafeEvent<EventPlayerDisconnect>(OnPlayerDisconnect));

        _hookTeamPre = Core.GameEvent.HookPre<EventPlayerTeam>(
            SafeEvent<EventPlayerTeam>(ev =>
            {
                ev.DontBroadcast = true;
                return HookResult.Continue;
            }));

        _hookTeam = Core.GameEvent.HookPost<EventPlayerTeam>(
            SafeEvent<EventPlayerTeam>(OnPlayerTeam));
    }

    private void UnregisterEvents()
    {
        foreach (var hook in new[] { _hookConnectFull, _hookDisconnectPre, _hookDisconnect, _hookTeamPre, _hookTeam })
        {
            if (hook.HasValue) Core.GameEvent.Unhook(hook.Value);
        }

        _hookConnectFull = null;
        _hookDisconnectPre = null;
        _hookDisconnect = null;
        _hookTeamPre = null;
        _hookTeam = null;
    }

    private IGameEventService.GameEventHandler<T> SafeEvent<T>(IGameEventService.GameEventHandler<T> handler)
        where T : IGameEvent<T>
        => ev =>
        {
            try
            {
                return handler(ev);
            }
            catch (Exception ex)
            {
                _logger.Error($"[EVENT] Обработчик {typeof(T).Name} упал", ex);
                return HookResult.Continue;
            }
        };
}
