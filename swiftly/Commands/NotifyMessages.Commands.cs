using System;
using System.Collections.Generic;
using System.Globalization;
using SwiftlyS2.Shared.Commands;

namespace NotifyMessages;

/// Частичный класс: команды (объявления/служебные).
///
/// Регистрируем не атрибутами, а вручную: команды нужно снимать в Unload, иначе после
/// выгрузки плагина в консоли остаются висеть их имена. registerRaw: false — SwiftlyS2
/// сам добавляет префикс sw_ (sw_servers, sw_reload_advert, ...).
public sealed partial class NotifyMessages
{
    /// Право на админские команды. В cssharp/ это было "@css/root".
    private const string AdminPermission = "notifymessages.admin";

    // Антиспам для sw_servers: команда доступна любому игроку и дёргает сетевые запросы,
    // поэтому без кулдауна её можно было использовать как усилитель нагрузки.
    private const double ServersCommandCooldownSeconds = 10.0;
    private readonly Dictionary<ulong, DateTime> _serversCommandCooldown = new();

    private readonly List<Guid> _commands = new();

    private void RegisterCommands()
    {
        _commands.Add(Core.Command.RegisterCommand("servers", OnServersCommand,
            registerRaw: false, permission: "",
            helpText: "Показать список серверов из кеша"));

        _commands.Add(Core.Command.RegisterCommand("restart_notify", OnRestartNotifyCommand,
            registerRaw: false, permission: AdminPermission,
            helpText: "Оповестить игроков о рестарте через N секунд (только из консоли сервера)"));

        _commands.Add(Core.Command.RegisterCommand("reload_advert", OnReloadCommand,
            registerRaw: false, permission: AdminPermission,
            helpText: "Перечитать все четыре конфига NotifyMessages"));

        _commands.Add(Core.Command.RegisterCommand("nm_preview", OnPreviewCommand,
            registerRaw: false, permission: AdminPermission,
            helpText: "Показать, как выглядит сообщение из конфига: " + PreviewUsage));

        _commands.Add(Core.Command.RegisterCommand("nm_check", OnCheckCommand,
            registerRaw: false, permission: AdminPermission,
            helpText: "Проверить все шаблоны конфигурации"));
    }

    private void UnregisterCommands()
    {
        foreach (var guid in _commands) Core.Command.UnregisterCommand(guid);
        _commands.Clear();
    }

    private void OnServersCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("[Servers] Команда доступна только игрокам");
            return;
        }

        var player = context.Sender;
        if (player is null || player.IsFakeClient || !player.IsValid) return;

        if (Config.Servers == null)
        {
            _logger.Debug("[COMMAND] Config.Servers is null");
            return;
        }

        if (!Config.Servers.Enabled)
        {
            context.Reply("[Servers] Server monitoring is disabled in Servers.json");
            return;
        }

        if (Config.Servers.List.Count == 0)
        {
            context.Reply("[Servers] No servers configured in Servers.json");
            return;
        }

        var steamId = player.SteamID;
        var now = DateTime.UtcNow;

        if (_serversCommandCooldown.TryGetValue(steamId, out var lastUse))
        {
            var elapsed = (now - lastUse).TotalSeconds;
            if (elapsed < ServersCommandCooldownSeconds)
            {
                var wait = Math.Ceiling(ServersCommandCooldownSeconds - elapsed);
                context.Reply($"[Servers] Please wait {wait.ToString("0", CultureInfo.InvariantCulture)} second(s) before using this command again.");
                return;
            }
        }

        _serversCommandCooldown[steamId] = now;

        _logger.Debug($"[COMMAND] sw_servers by {player.Name}, showing {Config.Servers.List.Count} server(s)");

        // Показываем текущие данные из кеша
        _serverStatusService.AnnounceToPlayer(player.Name,
            (channel, message) => _displayService.Print(channel, message, player));

        // И просим обновить кеш в фоне к следующему запросу (респектит TTL и in-flight guard)
        _serverStatusService.TriggerBackgroundUpdate();
    }

    /// Оповестить игроков о предстоящем рестарте/обновлении.
    ///
    /// Точка интеграции с внешним апдейтером: тот шлёт в консоль сервера
    /// `sw_restart_notify <секунды>` вместо голого `say <текст>` — и сообщение
    /// уходит игрокам с цветами и на их языке (Settings.RestartNotify + Messages.json).
    /// Только из консоли сервера — как SERVER_ONLY в cssharp/.
    private void OnRestartNotifyCommand(ICommandContext context)
    {
        if (context.IsSentByPlayer)
        {
            context.Reply("[NotifyMessages] sw_restart_notify принимается только из консоли сервера");
            return;
        }

        var arg = context.Args.Length > 0 ? context.Args[0] : "";
        if (!int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < 0 || seconds > 86400)
        {
            _logger.Info("[ERROR] Use: sw_restart_notify <seconds> (0-86400)");
            return;
        }

        var notify = Config.RestartNotify;
        if (notify is not { Enabled: true })
        {
            _logger.Debug("[COMMAND] sw_restart_notify skipped: RestartNotify disabled in Settings.json");
            return;
        }

        // Точная отсечка из конфига, иначе общий шаблон с {SECONDS}
        var template = notify.ResolveTemplate(seconds);

        if (string.IsNullOrEmpty(template))
        {
            _logger.Info("[COMMAND] sw_restart_notify: no message template configured");
            return;
        }

        // {SECONDS}/{TIME_RESTART} долетают и внутрь текстов из Messages.json:
        // ProcessMessage подставляет значения после локализации, но до рендера.
        var formattedTime = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{SECONDS}"] = seconds.ToString(CultureInfo.InvariantCulture),
            ["{TIME_RESTART}"] = formattedTime
        };

        _logger.Info($"[COMMAND] sw_restart_notify {seconds}s -> {notify.MessageType}");
        _displayService.Print(notify.MessageType, template, null, values);
    }

    private void OnReloadCommand(ICommandContext context)
    {
        _logger.Info($"[COMMAND] sw_reload_advert executed by {(context.IsSentByPlayer ? context.Sender?.Name : null) ?? "Console"}");

        Config = LoadConfigSafely();
        // Индекс языков кеширует Config — пересобираем, иначе останется на старом конфиге
        _languageIndex = LanguageIndex.Build(Config);
        _messageProcessor = new MessageProcessor(Config, ResolveLanguage, _serverInfo);
        _displayService.Update(Config, _messageProcessor);

        // Пересоздаём сервисы и таймеры, чтобы применить новый конфиг
        try { _serverStatusService?.Stop(); } catch (Exception) { /* пересоздаём в любом случае */ }
        try { _advertisementService?.Stop(); } catch (Exception) { /* пересоздаём в любом случае */ }

        _serversCommandCooldown.Clear();

        _serverStatusService = CreateServerStatusService();
        _advertisementService = CreateAdvertisementService();

        CacheGeoForEveryone();

        _serverStatusService.InitialQuery();
        _advertisementService.Start();
        _serverStatusService.Start();

        context.Reply("[NotifyMessages] configuration successfully reloaded!");
    }
}
