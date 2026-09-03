using System;
using System.Collections.Generic;
using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace NotifyMessages;

/// Частичный класс: команды (объявления/служебные)
public partial class NotifyMessages
{
    // Антиспам для css_servers: команда доступна любому игроку и дёргает сетевые запросы,
    // поэтому без кулдауна её можно было использовать как усилитель нагрузки.
    private const double ServersCommandCooldownSeconds = 10.0;
    private readonly Dictionary<ulong, DateTime> _serversCommandCooldown = new();

    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    [ConsoleCommand("css_servers", "Показать список серверов из кеша")]
    public void ShowServersCommand(CCSPlayerController? controller, CommandInfo command)
    {
        if (controller is not { IsValid: true, IsBot: false }) return;

        if (Config.Servers == null)
        {
            _logger.Debug("[COMMAND] Config.Servers is null");
            return;
        }

        if (!Config.Servers.Enabled)
        {
            controller.PrintToChat("[Servers] Server monitoring is disabled in Servers.json");
            return;
        }

        if (Config.Servers.List.Count == 0)
        {
            controller.PrintToChat("[Servers] No servers configured in Servers.json");
            return;
        }

        var steamId = controller.SteamID;
        var now = DateTime.UtcNow;

        if (_serversCommandCooldown.TryGetValue(steamId, out var lastUse))
        {
            var elapsed = (now - lastUse).TotalSeconds;
            if (elapsed < ServersCommandCooldownSeconds)
            {
                var wait = Math.Ceiling(ServersCommandCooldownSeconds - elapsed);
                controller.PrintToChat($"[Servers] Please wait {wait:0} second(s) before using this command again.");
                return;
            }
        }

        _serversCommandCooldown[steamId] = now;

        _logger.Debug($"[COMMAND] css_servers by {controller.PlayerName}, showing {Config.Servers.List.Count} server(s)");

        // Показываем текущие данные из кеша
        _serverStatusService.AnnounceToPlayer(controller, _messageProcessor, _displayService.Print);

        // И просим обновить кеш в фоне к следующему запросу (респектит TTL и in-flight guard)
        _serverStatusService.TriggerBackgroundUpdate();
    }

    [CommandHelper(minArgs: 1, usage: "<seconds>", whoCanExecute: CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_announce_restart", "Сказать всем, что будет рестарт через N секунд")]
    public void AnnounceRestart(CCSPlayerController? controller, CommandInfo command)
    {
        AnnounceTimed(controller, command, Config.RestartMessage, "css_announce_restart");
    }

    [CommandHelper(minArgs: 1, usage: "<seconds>", whoCanExecute: CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_announce_update", "Сказать всем, что будет обновление через N секунд")]
    public void AnnounceUpdate(CCSPlayerController? controller, CommandInfo command)
    {
        AnnounceTimed(controller, command, Config.UpdateMessage, "css_announce_update");
    }

    private void AnnounceTimed(CCSPlayerController? controller, CommandInfo command, string? template,
        string commandName)
    {
        if (!int.TryParse(command.GetArg(1), out var seconds) || seconds <= 0 || seconds > 3600)
        {
            var usage = $"[ERROR] Use: {commandName} <seconds> (1-3600)";
            if (controller != null) controller.PrintToChat(usage);
            else _logger.Info(usage);
            return;
        }

        if (string.IsNullOrEmpty(template))
        {
            _logger.Info($"[COMMAND] {commandName}: message template is not configured in Settings.json");
            return;
        }

        var formattedTime = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        var message = _messageProcessor.ProcessMessage(template, 0).Replace("{TIME_RESTART}", formattedTime);

        _displayService.Print(HudDestination.Chat, message);
    }

    /// Оповестить игроков о предстоящем рестарте/обновлении.
    ///
    /// Точка интеграции с внешним апдейтером: тот шлёт в консоль сервера
    /// `css_restart_notify <секунды>` вместо голого `say <текст>` — и сообщение
    /// уходит игрокам с цветами и на их языке (Settings.RestartNotify + Messages.json).
    [CommandHelper(minArgs: 1, usage: "<seconds>", whoCanExecute: CommandUsage.SERVER_ONLY)]
    [ConsoleCommand("css_restart_notify", "Оповестить игроков о рестарте через N секунд")]
    public void RestartNotifyCommand(CCSPlayerController? controller, CommandInfo command)
    {
        if (!int.TryParse(command.GetArg(1), out var seconds) || seconds < 0 || seconds > 86400)
        {
            _logger.Info("[ERROR] Use: css_restart_notify <seconds> (0-86400)");
            return;
        }

        var notify = Config.RestartNotify;
        if (notify is not { Enabled: true })
        {
            _logger.Debug("[COMMAND] css_restart_notify skipped: RestartNotify disabled in Settings.json");
            return;
        }

        // Точная отсечка из конфига, иначе общий шаблон с {SECONDS}
        var template = notify.ResolveTemplate(seconds);

        if (string.IsNullOrEmpty(template))
        {
            _logger.Info("[COMMAND] css_restart_notify: no message template configured");
            return;
        }

        // {SECONDS}/{TIME_RESTART} подставляем ДО ProcessMessage, чтобы они долетели
        // и внутрь текстов из Messages.json
        var formattedTime = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        var message = template
            .Replace("{SECONDS}", seconds.ToString(CultureInfo.InvariantCulture))
            .Replace("{TIME_RESTART}", formattedTime);

        var destination = DisplayService.ToHudDestination(notify.MessageType);

        _logger.Info($"[COMMAND] css_restart_notify {seconds}s -> {destination}");
        _displayService.Print(destination, message);
    }

    [RequiresPermissions("@css/root")]
    [ConsoleCommand("css_reload_advert", "Reload all configuration files")]
    public void ReloadAdvertConfig(CCSPlayerController? controller, CommandInfo command)
    {
        _logger.Info($"[COMMAND] css_reload_advert executed by {controller?.PlayerName ?? "Console"}");

        Config = _configService.LoadOrCreate(Application.RootDirectory);
        _messageProcessor = new MessageProcessor(Config,
            steamId => _geoIpService.GetIsoForSteamId(steamId) ?? Config.DefaultLang);
        _displayService.Update(Config, _messageProcessor);

        // Re-init services and timers to apply new config
        try { _serverStatusService?.Stop(); } catch { /* ignore */ }
        try { _advertisementService?.Stop(); } catch { /* ignore */ }

        _serversCommandCooldown.Clear();

        _serverStatusService = CreateServerStatusService();
        _advertisementService = CreateAdvertisementService();

        foreach (var player in Utilities.GetPlayers())
        {
            if (player is not { IsValid: true, IsBot: false }) continue;

            var ip = GeoIpService.ExtractIp(player.IpAddress);
            if (string.IsNullOrEmpty(ip)) continue;

            var defaultLang = Config.DefaultLang ?? string.Empty;
            _geoIpService.UpdatePlayerCache(player.SteamID, ip, defaultLang);
        }

        _serverStatusService.InitialQuery();
        _advertisementService.Start();
        _serverStatusService.Start();

        const string msg = "[NotifyMessages] configuration successfully reloaded!";
        if (controller == null) _logger.Info(msg);
        else controller.PrintToChat(msg);
    }
}
