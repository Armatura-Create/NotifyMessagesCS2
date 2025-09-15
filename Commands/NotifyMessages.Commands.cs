using System;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace NotifyMessages;

/// Частичный класс: команды (объявления/служебные)
public partial class NotifyMessages
{
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    [ConsoleCommand("css_servers", "Показать список серверов из кеша")]
    public void ShowServersCommand(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller == null) return;
        if (Config.Servers == null || !Config.Servers.Enabled || Config.Servers.List.Count == 0) return;

        AnnounceServersToPlayer(controller);
    }

    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_announce_restart", "Сказать всем, что будет рестарт через N секунд")]
    public void AnnounceRestart(CCSPlayerController? controller, CommandInfo command)
    {
        if (command.ArgCount < 2 || !int.TryParse(command.ArgString, out var seconds) || seconds <= 0)
        {
            controller?.PrintToChat("[ERROR] Use: css_announce_restart <seconds>");
            return;
        }

        if (string.IsNullOrEmpty(Config.RestartMessage)) return;

        var timeSpan = TimeSpan.FromSeconds(seconds);
        var formattedTime = timeSpan.ToString(@"mm\:ss");
        var restartMessage = _messageProcessor.ProcessMessage(Config.RestartMessage, 0).Replace("{TIME_RESTART}", formattedTime);

        PrintWrappedLine(HudDestination.Chat, restartMessage);
    }

    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_announce_update", "Сказать всем, что будет обновлен через N секунд")]
    public void AnnounceUpdate(CCSPlayerController? controller, CommandInfo command)
    {
        if (command.ArgCount < 2 || !int.TryParse(command.ArgString, out var seconds) || seconds <= 0)
        {
            controller?.PrintToChat("[ERROR] Use: css_announce_update <seconds>");
            return;
        }

        if (string.IsNullOrEmpty(Config.UpdateMessage)) return;

        var timeSpan = TimeSpan.FromSeconds(seconds);
        var formattedTime = timeSpan.ToString(@"mm\:ss");
        var restartMessage = _messageProcessor.ProcessMessage(Config.UpdateMessage, 0).Replace("{TIME_RESTART}", formattedTime);

        PrintWrappedLine(HudDestination.Chat, restartMessage);
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_advert_reload", "configuration restart")]
    public void ReloadAdvertConfig(CCSPlayerController? controller, CommandInfo command)
    {
        Config = LoadConfig();

        // Re-init services and timers to apply new config
        try { _serverStatusService?.Stop(); } catch { /* ignore */ }
        _serverStatusService = new ServerStatusService(
            Config,
            _logger,
            (interval, action) => AddTimer(interval, action),
            (interval, action, flags) => AddTimer(interval, action, flags),
            action => AddTimer(0.0f, action));

        foreach (var t in _timers) t.Kill();
        _timers.Clear();

        foreach (var t in _serverTimers) t.Kill();
        _serverTimers.Clear();

        lock (_serverCacheLock) _serverCache.Clear();

        InitialServerQuery();
        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IpAddress == null || player.IsBot || !player.IsValid)
                continue;

            var ip = player.IpAddress.Split(':')[0];
            var defaultLang = Config.DefaultLang ?? string.Empty;
            _playerIsoCode[player.SteamID] = _geoIpService.GetIsoCode(ip, defaultLang);
            _playerCity[player.SteamID] = _geoIpService.GetCity(ip);
        }

        InitialServerQuery();
        StartTimers();
        StartServerTimers();

        const string msg = "[Advertisement] configuration successfully rebooted!";
        if (controller == null) _logger.Info(msg);
        else controller.PrintToChat(msg);
    }
}
