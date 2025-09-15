using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;

namespace NotifyMessages;

/// Регистрация всех игровых событий и листенеров в отдельном частичном классе
public partial class NotifyMessages
{
    private void RegisterEvents()
    {
        // Game events
        RegisterEventHandler<EventPlayerConnectFull>(EventPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(EventPlayerDisconnectPre, HookMode.Pre);
        RegisterEventHandler<EventPlayerDisconnect>(EventPlayerDisconnect);
        RegisterEventHandler<EventPlayerTeam>(EventPlayerTeamChangePre, HookMode.Pre);
        RegisterEventHandler<EventPlayerTeam>(EventPlayerTeamChange);

        // Listeners
        RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
        RegisterListener<Listeners.OnTick>(OnTick);
    }
}
