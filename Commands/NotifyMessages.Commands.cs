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
        
        _logger.Debug($"[COMMAND] css_servers called by {controller.PlayerName}");
        
        if (Config.Servers == null)
        {
            _logger.Debug($"[COMMAND] Config.Servers is null");
            return;
        }
        
        if (!Config.Servers.Enabled)
        {
            _logger.Debug($"[COMMAND] Config.Servers.Enabled = false");
            controller.PrintToChat("[Servers] Server monitoring is disabled in Servers.json");
            return;
        }
        
        if (Config.Servers.List.Count == 0)
        {
            _logger.Debug($"[COMMAND] Config.Servers.List is empty");
            controller.PrintToChat("[Servers] No servers configured in Servers.json");
            return;
        }

        _logger.Debug($"[COMMAND] Showing {Config.Servers.List.Count} server(s) from cache");

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
    [ConsoleCommand("css_reload_advert", "Reload all configuration files")]
    public void ReloadAdvertConfig(CCSPlayerController? controller, CommandInfo command)
    {
        _logger.Info($"[COMMAND] css_reload_advert executed by {controller?.PlayerName ?? "Console"}");
        
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
