using System;
using CounterStrikeSharp.API.Core;

namespace NotifyMessages;

/// Регистрация всех игровых событий и листенеров в отдельном частичном классе
public partial class NotifyMessages
{
    private void RegisterEvents()
    {
        // Каждый обработчик обёрнут: исключение в одном событии не должно всплывать
        // во фреймворк и мешать другим плагинам обрабатывать то же событие.
        RegisterEventHandler<EventPlayerConnectFull>((ev, info) =>
            SafeEvent(() => EventPlayerConnectFull(ev, info), nameof(EventPlayerConnectFull)));

        RegisterEventHandler<EventPlayerDisconnect>((ev, info) =>
            SafeEvent(() => EventPlayerDisconnectPre(ev, info), nameof(EventPlayerDisconnectPre)), HookMode.Pre);

        RegisterEventHandler<EventPlayerDisconnect>((ev, info) =>
            SafeEvent(() => EventPlayerDisconnect(ev, info), nameof(EventPlayerDisconnect)));

        RegisterEventHandler<EventPlayerTeam>((ev, info) =>
            SafeEvent(() => EventPlayerTeamChangePre(ev, info), nameof(EventPlayerTeamChangePre)), HookMode.Pre);

        RegisterEventHandler<EventPlayerTeam>((ev, info) =>
            SafeEvent(() => EventPlayerTeamChange(ev, info), nameof(EventPlayerTeamChange)));

        // Listeners
        RegisterListener<Listeners.OnClientAuthorized>((slot, id) =>
            SafeEvent(() => { OnClientAuthorized(slot, id); return HookResult.Continue; }, nameof(OnClientAuthorized)));

        RegisterListener<Listeners.OnTick>(OnTickSafe);
    }

    private HookResult SafeEvent(Func<HookResult> body, string name)
    {
        try
        {
            return body();
        }
        catch (Exception ex)
        {
            _logger.Error($"[EVENT] Обработчик {name} упал", ex);
            return HookResult.Continue;
        }
    }

    /// OnTick зовётся 64 раза в секунду: логировать одну и ту же ошибку каждый тик нельзя.
    /// При сбое гасим HTML-центр целиком — это единственное, что здесь работает, — и молчим,
    /// пока не появится новое сообщение.
    private void OnTickSafe()
    {
        try
        {
            OnTick();
        }
        catch (Exception ex)
        {
            _logger.Error("[EVENT] OnTick упал, HTML-центр очищен до следующего сообщения", ex);
            try { _displayService.ClearUsers(); } catch { /* ignore */ }
        }
    }
}
