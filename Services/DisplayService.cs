using System;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Server = CounterStrikeSharp.API.Server;

namespace NotifyMessages;

/// Сервис вывода сообщений и управления HTML-центром
public sealed class DisplayService
{
    // Если HtmlCenterDuration не задан в конфиге — показываем столько секунд.
    // Раньше null означал "погасить на первом же тике", т.е. HTML-центр не работал вовсе.
    private const float DefaultHtmlDurationSeconds = 5f;

    private Config _config;
    private MessageProcessor _messageProcessor;
    private readonly ILogger _logger;

    // Пользовательские данные по слотам (для HTML center)
    private readonly User?[] _users;

    // Сколько слотов сейчас реально требуют перерисовки. Пока 0 — OnTick выходит сразу.
    private int _htmlActiveCount;

    public DisplayService(Config config, MessageProcessor messageProcessor, ILogger logger)
    {
        _config = config;
        _messageProcessor = messageProcessor;
        _logger = logger;

        // Размер берём от сервера, а не константой 66: на нестандартном maxplayers
        // индексация по player.Slot выходила за границы массива.
        var slots = Math.Max(65, Server.MaxPlayers + 1);
        _users = new User?[slots];

        _logger.Debug($"[DisplayService] Initialized with Debug={_config.Debug}, slots={slots}");
    }

    public void Update(Config config, MessageProcessor messageProcessor)
    {
        // Обновление конфигурации и процессора при перезагрузке
        // Состояние пользователей сохраняем
        _config = config;
        _messageProcessor = messageProcessor;

        _logger.Debug($"[DisplayService] Config updated, Debug={_config.Debug}");
    }

    /// Унифицированный вывод строки с обработкой локализации/цветов.
    /// target == null — сообщение всем игрокам, иначе только указанному.
    public void Print(HudDestination? destination, string message, CCSPlayerController? target = null)
    {
        if (string.IsNullOrEmpty(message)) return;

        if (target != null)
        {
            if (target is not { IsValid: true, IsBot: false }) return;

            var processed = _messageProcessor.ProcessMessage(message, target.SteamID);
            SendTo(target, destination, processed);

            if (_config.Debug)
            {
                var isoCode = _messageProcessor.GetIsoCodeBySteamId(target.SteamID) ?? _config.DefaultLang ?? "default";
                _logger.Debug(
                    $"[{DestinationName(destination)}] -> player '{target.PlayerName}' ({isoCode}): {TextFormatter.StripColorCodes(processed)}");
            }

            return;
        }

        // Кешируем обработанные сообщения по ISO-коду: обработка идёт один раз на язык,
        // а не на каждого игрока.
        var processedMessages = new Dictionary<string, string>();
        var playerCount = 0;

        foreach (var player in Utilities.GetPlayers())
        {
            if (player is not { IsValid: true, IsBot: false }) continue;

            playerCount++;

            var isoCode = _messageProcessor.GetIsoCodeBySteamId(player.SteamID) ?? _config.DefaultLang ?? "default";

            if (!processedMessages.TryGetValue(isoCode, out var processed))
            {
                processed = _messageProcessor.ProcessMessage(message, player.SteamID);
                processedMessages[isoCode] = processed;
            }

            SendTo(player, destination, processed);
        }

        if (!_config.Debug) return;

        var destinationType = DestinationName(destination);
        if (playerCount > 0)
        {
            _logger.Debug($"[{destinationType}] -> {playerCount} player(s), {processedMessages.Count} language(s):");
            foreach (var (isoCode, processed) in processedMessages)
                _logger.Debug($"  [{isoCode}] {TextFormatter.StripColorCodes(processed)}");
        }
        else
        {
            // Игроков нет — показываем, как сообщение выглядело бы на языке по умолчанию
            var processed = _messageProcessor.ProcessMessage(message, 0);
            var defaultLang = _config.DefaultLang ?? "default";
            _logger.Debug(
                $"[{destinationType}] -> No valid players online. Message [{defaultLang}]: {TextFormatter.StripColorCodes(processed)}");
        }
    }

    // Вызывается каждый тик для поддержки HTML center
    public void OnTick()
    {
        // Ничего не показываем — не трогаем ни игроков, ни нативы.
        if (_htmlActiveCount <= 0) return;

        var tickInterval = Server.TickInterval;
        if (tickInterval <= 0f) tickInterval = 1f / 64f;

        var duration = _config.HtmlCenterDuration ?? DefaultHtmlDurationSeconds;
        var showWhenDead = _config.ShowHtmlWhenDead ?? false;

        for (var slot = 0; slot < _users.Length; slot++)
        {
            var user = _users[slot];
            if (user is not { HtmlPrint: true }) continue;

            var player = Utilities.GetPlayerFromSlot(slot);
            if (player is not { IsValid: true })
            {
                // Игрок ушёл — гасим слот, иначе счётчик активных никогда не обнулится
                Deactivate(slot);
                continue;
            }

            if (user.PrintTime * tickInterval >= duration)
            {
                Deactivate(slot);
                continue;
            }

            if (!showWhenDead && !player.PawnIsAlive)
                continue; // пауза: таймер не тикает, пока игрок мёртв

            player.PrintToCenterHtml(user.Message);
            user.PrintTime++;
        }
    }

    public void ClearUsers()
    {
        for (var i = 0; i < _users.Length; i++) _users[i] = null;
        _htmlActiveCount = 0;
    }

    /// Сбросить HTML-состояние конкретного слота (вызывается при отключении игрока)
    public void ClearUser(int slot)
    {
        if ((uint)slot >= (uint)_users.Length) return;
        Deactivate(slot);
        _users[slot] = null;
    }

    private void SendTo(CCSPlayerController player, HudDestination? destination, string processed)
    {
        switch (destination)
        {
            case HudDestination.Chat:
                player.PrintToChat(TextFormatter.EnsureChatColorPrefix(processed));
                break;
            case HudDestination.Console:
                player.PrintToConsole(processed);
                break;
            case HudDestination.Alert:
                player.PrintToCenterAlert(processed);
                break;
            default:
                if (_config.PrintToCenterHtml == true)
                    SetHtmlPrintSettings(player, processed);
                else
                    player.PrintToCenter(processed);
                break;
        }
    }

    private static string DestinationName(HudDestination? destination) => destination switch
    {
        HudDestination.Chat => "CHAT",
        HudDestination.Center => "CENTER",
        HudDestination.Console => "CONSOLE",
        HudDestination.Alert => "ALERT",
        _ => "UNKNOWN"
    };

    /// Единый маппинг MessageType из конфига в канал вывода.
    /// CenterHtml сознательно ложится в Center: HTML включается глобальным
    /// Settings.PrintToCenterHtml, отдельного канала под него в HudDestination нет.
    public static HudDestination ToHudDestination(MessageType type) => type switch
    {
        MessageType.Chat => HudDestination.Chat,
        MessageType.Console => HudDestination.Console,
        MessageType.Alert => HudDestination.Alert,
        _ => HudDestination.Center
    };

    private void Deactivate(int slot)
    {
        var user = _users[slot];
        if (user is not { HtmlPrint: true }) return;
        user.HtmlPrint = false;
        if (_htmlActiveCount > 0) _htmlActiveCount--;
    }

    private void SetHtmlPrintSettings(CCSPlayerController player, string message)
    {
        var slot = player.Slot;
        if ((uint)slot >= (uint)_users.Length)
        {
            _logger.Error($"[DisplayService] Player slot {slot} out of range ({_users.Length}), HTML center skipped");
            return;
        }

        var user = _users[slot];
        if (user == null)
        {
            user = new User();
            _users[slot] = user;
        }

        if (!user.HtmlPrint) _htmlActiveCount++;
        user.HtmlPrint = true;
        user.PrintTime = 0;
        user.Message = message;
    }
}
