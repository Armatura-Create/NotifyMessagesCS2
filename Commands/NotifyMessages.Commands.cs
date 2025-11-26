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

        if (Config.Debug)
        {
            _logger.Info($"[COMMAND] css_servers executed by {controller.PlayerName}");
        }

        // Показываем текущие данные из кеша
        _serverStatusService.AnnounceToPlayer(controller, _messageProcessor, _displayService.Print);
        
        // Запускаем фоновое обновление кеша для следующего запроса
        _serverStatusService.TriggerBackgroundUpdate();
    }

    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_announce_restart", "Сказать всем, что будет рестарт через N секунд")]
    public void AnnounceRestart(CCSPlayerController? controller, CommandInfo command)
    {
        if (command.ArgCount < 2 || !int.TryParse(command.ArgString, out var seconds) || seconds <= 0 || seconds > 3600)
        {
            controller?.PrintToChat("[ERROR] Use: css_announce_restart <seconds> (1-3600)");
            return;
        }

        if (string.IsNullOrEmpty(Config.RestartMessage)) return;

        var timeSpan = TimeSpan.FromSeconds(seconds);
        var formattedTime = timeSpan.ToString(@"mm\:ss");
        var restartMessage = _messageProcessor.ProcessMessage(Config.RestartMessage, 0).Replace("{TIME_RESTART}", formattedTime);

        _displayService.Print(HudDestination.Chat, restartMessage, null, false);
    }

    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_announce_update", "Сказать всем, что будет обновлен через N секунд")]
    public void AnnounceUpdate(CCSPlayerController? controller, CommandInfo command)
    {
        if (command.ArgCount < 2 || !int.TryParse(command.ArgString, out var seconds) || seconds <= 0 || seconds > 3600)
        {
            controller?.PrintToChat("[ERROR] Use: css_announce_update <seconds> (1-3600)");
            return;
        }

        if (string.IsNullOrEmpty(Config.UpdateMessage)) return;

        var timeSpan = TimeSpan.FromSeconds(seconds);
        var formattedTime = timeSpan.ToString(@"mm\:ss");
        var restartMessage = _messageProcessor.ProcessMessage(Config.UpdateMessage, 0).Replace("{TIME_RESTART}", formattedTime);

        _displayService.Print(HudDestination.Chat, restartMessage, null, false);
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_advert_reload", "configuration restart")]
    public void ReloadAdvertConfig(CCSPlayerController? controller, CommandInfo command)
    {
        _logger.Info($"[COMMAND] css_advert_reload executed by {controller?.PlayerName ?? "Console"}");
        
        Config = _configService.LoadOrCreate(Application.RootDirectory);
        _messageProcessor = new MessageProcessor(Config, steamId => _geoIpService.GetIsoForSteamId(steamId) ?? Config.DefaultLang);
        _displayService.Update(Config, _messageProcessor);

        // Re-init services and timers to apply new config
        try { _serverStatusService?.Stop(); } catch { /* ignore */ }
        _serverStatusService = new ServerStatusService(
            Config,
            _logger,
            (interval, action) => AddTimer(interval, action),
            (interval, action, flags) => AddTimer(interval, action, flags),
            action => AddTimer(0.0f, action));

        try { _advertisementService?.Stop(); } catch { /* ignore */ }
        _advertisementService = new AdvertisementService(
            Config,
            _messageProcessor,
            _logger,
            (interval, action, flags) => AddTimer(interval, action, flags),
            _displayService.Print);

        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IpAddress == null || player.IsBot || !player.IsValid)
                continue;

            var ip = player.IpAddress.Split(':')[0];
            var defaultLang = Config.DefaultLang ?? string.Empty;
            _geoIpService.UpdatePlayerCache(player.SteamID, ip, defaultLang);
        }

        _serverStatusService.InitialQuery();
        _advertisementService.Start();
        _serverStatusService.Start();

        const string msg = "[Advertisement] configuration successfully rebooted!";
        if (controller == null) _logger.Info(msg);
        else controller.PrintToChat(msg);
    }
}
