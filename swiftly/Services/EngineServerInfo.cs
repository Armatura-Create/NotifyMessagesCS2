using SwiftlyS2.Shared;

namespace NotifyMessages;

/// Факты о сервере из движка через SwiftlyS2.
///
/// Каждое свойство — натив. Читать только из главного потока и только после Load():
/// конструктор лишь запоминает Core, чтобы сервис можно было создать до готовности движка.
public sealed class EngineServerInfo : IServerInfoSource
{
    private readonly ISwiftlyCore _core;

    public EngineServerInfo(ISwiftlyCore core)
    {
        _core = core;
    }

    public string MapName => _core.Engine.GlobalVars.MapName.Value ?? "";

    public string Hostname => _core.ConVar.FindAsString("hostname")?.ValueAsString ?? "Server";

    public string Ip => _core.ConVar.FindAsString("ip")?.ValueAsString ?? "127.0.0.1";

    public string Port => _core.ConVar.FindAsString("hostport")?.ValueAsString ?? "27015";

    public int MaxPlayers => _core.PlayerManager.MaxPlayers;

    public int Players
    {
        get
        {
            var count = 0;
            foreach (var player in _core.PlayerManager.GetAllPlayers())
            {
                if (!player.IsFakeClient && player.IsValid) count++;
            }

            return count;
        }
    }
}
