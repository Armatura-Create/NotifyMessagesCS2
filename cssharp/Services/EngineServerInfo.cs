using System.Globalization;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using Server = CounterStrikeSharp.API.Server;

namespace NotifyMessages;

/// Факты о сервере из движка через CounterStrikeSharp.
///
/// Каждое свойство — натив. Читать только из главного потока и только после Load():
/// конструктор пуст сознательно, чтобы сервис можно было создать до готовности движка.
public sealed class EngineServerInfo : IServerInfoSource
{
    public string MapName => NativeAPI.GetMapName();

    public string Hostname => ConVar.Find("hostname")?.StringValue ?? "Server";

    public string Ip => ConVar.Find("ip")?.StringValue ?? "127.0.0.1";

    public string Port => ConVar.Find("hostport")?.GetPrimitiveValue<int>().ToString(CultureInfo.InvariantCulture)
                          ?? "27015";

    public int MaxPlayers => Server.MaxPlayers;

    public int Players => Utilities.GetPlayers().Count(u => u.PlayerPawn?.Value?.IsValid == true);
}
