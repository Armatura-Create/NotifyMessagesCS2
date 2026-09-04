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

    // Размер массива слотов. Константа, а НЕ Server.MaxPlayers: сервис создаётся в Load(),
    // где нативы движка ещё недоступны ("Global Variables not initialized yet").
    // CS2 поддерживает до 64 игроков; запас взят с большим излишком, это ~1 КБ ссылок.
    // От выхода за границы страхует проверка в SetHtmlPrintSettings и в OnTick.
    private const int MaxSlots = 128;

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

        _users = new User?[MaxSlots];

        _logger.Debug($"[DisplayService] Initialized with Debug={_config.Debug}, slots={MaxSlots}");
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

    /// Вызывается каждый тик для поддержки HTML center.
    ///
    /// Обход ТОЛЬКО через Utilities.GetPlayers(): он фильтрует по IsValid и Connected.
    /// Utilities.GetPlayerFromSlot(slot) для этого не годится — внутри он делает
    /// new CCSPlayerController(EntitySystem.GetEntityByIndex(slot + 1)) БЕЗ проверки типа,
    /// то есть для освобождённого или переиспользованного индекса возвращает чужую
    /// сущность. Чтение PawnIsAlive у неё уходит по неверным смещениям и роняет сервер
    /// без единой строки в консоли.
    public void OnTick()
    {
        // Ничего не показываем — не трогаем ни игроков, ни нативы.
        if (_htmlActiveCount <= 0) return;

        var tickInterval = Server.TickInterval;
        if (tickInterval <= 0f) tickInterval = 1f / 64f;

        var duration = _config.HtmlCenterDuration ?? DefaultHtmlDurationSeconds;
        var showWhenDead = _config.ShowHtmlWhenDead ?? false;

        var stillActive = 0;

        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsBot) continue;

            var slot = player.Slot;
            if ((uint)slot >= (uint)_users.Length) continue;

            var user = _users[slot];
            if (user is not { HtmlPrint: true }) continue;

            if (ShouldStopShowing(user, player.SteamID, user.PrintTime * tickInterval, duration))
            {
                user.HtmlPrint = false;
                continue;
            }

            if (!showWhenDead && !player.PawnIsAlive)
            {
                stillActive++; // пауза: таймер не тикает, пока игрок мёртв
                continue;
            }

            player.PrintToCenterHtml(user.Message);
            user.PrintTime++;
            stillActive++;
        }

        // Счётчик пересчитывается по факту, а не ведётся вручную: слот игрока, который
        // отвалился по таймауту (без события disconnect), иначе залипал бы навсегда.
        _htmlActiveCount = stillActive;
    }

    /// Пора ли гасить сообщение: истекло время или слот уже достался другому игроку.
    internal static bool ShouldStopShowing(User user, ulong currentSteamId, float elapsedSeconds,
        float durationSeconds)
        => user.SteamId != currentSteamId || elapsedSeconds >= durationSeconds;

    public void ClearUsers()
    {
        for (var i = 0; i < _users.Length; i++) _users[i] = null;
        _htmlActiveCount = 0;
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
        user.SteamId = player.SteamID;
    }
}
