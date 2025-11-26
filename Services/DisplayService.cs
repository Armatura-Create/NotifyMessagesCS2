using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace NotifyMessages;

/// Сервис вывода сообщений и управления HTML-центром
public sealed class DisplayService
{
    private Config _config;
    private MessageProcessor _messageProcessor;
    private readonly ILogger _logger;

    // Пользовательские данные по слотам (для HTML center)
    private readonly User?[] _users = new User?[66];

    public DisplayService(Config config, MessageProcessor messageProcessor, ILogger logger)
    {
        _config = config;
        _messageProcessor = messageProcessor;
        _logger = logger;
        
        // Диагностическое сообщение для проверки Debug флага
        _logger.Debug($"[DisplayService] Initialized with Debug={_config.Debug}");
    }

    public void Update(Config config, MessageProcessor messageProcessor)
    {
        // Обновление конфигурации и процессора при перезагрузке
        // Состояние пользователей сохраняем
        _config = config;
        _messageProcessor = messageProcessor;
        
        // Диагностическое сообщение для проверки Debug флага после обновления
        _logger.Debug($"[DisplayService] Config updated, Debug={_config.Debug}");
    }

    // Унифицированный вывод строки с обработкой локализации/цветов
    public void Print(HudDestination? destination, string message,
        CCSPlayerController? connectPlayer = null, bool privateMsg = false)
    {
        if (connectPlayer != null && connectPlayer is { IsValid: true, IsBot: false } && privateMsg)
        {
            var processed = _messageProcessor.ProcessMessage(message, connectPlayer.SteamID);

            switch (destination)
            {
                case HudDestination.Chat:
                    processed = TextFormatter.EnsureChatColorPrefix(processed);
                    connectPlayer.PrintToChat(processed);
                    break;
                case HudDestination.Console:
                    connectPlayer.PrintToConsole(processed);
                    break;
                default:
                    if (_config.PrintToCenterHtml == true)
                        SetHtmlPrintSettings(connectPlayer, processed);
                    else
                        connectPlayer.PrintToCenter(processed);
                    break;
            }

            // Debug: показываем обработанное сообщение для конкретного игрока
            if (_config.Debug)
            {
                var destinationType = destination switch
                {
                    HudDestination.Chat => "CHAT",
                    HudDestination.Center => "CENTER",
                    HudDestination.Console => "CONSOLE",
                    _ => "UNKNOWN"
                };
                
                var cleanProcessed = TextFormatter.StripColorCodes(processed);
                var isoCode = _messageProcessor.GetIsoCodeBySteamId(connectPlayer.SteamID) ?? _config.DefaultLang ?? "default";
                _logger.Debug($"[{destinationType}] → player '{connectPlayer.PlayerName}' ({isoCode}): {cleanProcessed}");
            }
        }
        else
        {
            // Кешируем обработанные сообщения по ISO-коду для оптимизации
            var processedMessages = new Dictionary<string, string>();
            var playerCount = 0;
            
            foreach (var player in Utilities.GetPlayers().Where(u => !privateMsg && !u.IsBot && u.IsValid))
            {
                playerCount++;
                
                // Получаем ISO-код для кеширования
                var isoCode = _messageProcessor.GetIsoCodeBySteamId(player.SteamID) ?? _config.DefaultLang ?? "default";
                
                if (!processedMessages.TryGetValue(isoCode, out var processed))
                {
                    processed = _messageProcessor.ProcessMessage(message, player.SteamID);
                    processedMessages[isoCode] = processed;
                }

                switch (destination)
                {
                    case HudDestination.Chat:
                        processed = TextFormatter.EnsureChatColorPrefix(processed);
                        player.PrintToChat(processed);
                        break;
                    case HudDestination.Console:
                        player.PrintToConsole(processed);
                        break;
                    default:
                        if (_config.PrintToCenterHtml == true)
                            SetHtmlPrintSettings(player, processed);
                        else
                            player.PrintToCenter(processed);
                        break;
                }
            }

            // Debug: показываем обработанные сообщения и статистику отправки
            if (_config.Debug)
            {
                var destinationType = destination switch
                {
                    HudDestination.Chat => "CHAT",
                    HudDestination.Center => "CENTER",
                    HudDestination.Console => "CONSOLE",
                    _ => "UNKNOWN"
                };
                
                if (playerCount > 0)
                {
                    _logger.Debug($"[{destinationType}] → {playerCount} player(s), {processedMessages.Count} language(s):");
                    
                    foreach (var (isoCode, processed) in processedMessages)
                    {
                        var cleanProcessed = TextFormatter.StripColorCodes(processed);
                        _logger.Debug($"  [{isoCode}] {cleanProcessed}");
                    }
                }
                else
                {
                    // Показываем что сообщение было вызвано, но игроков нет
                    // Обрабатываем с дефолтным языком для демонстрации
                    var processed = _messageProcessor.ProcessMessage(message, 0); // 0 = используется DefaultLang
                    var cleanMessage = TextFormatter.StripColorCodes(processed);
                    var defaultLang = _config.DefaultLang ?? "default";
                    _logger.Debug($"[{destinationType}] → No valid players online. Message [{defaultLang}]: {cleanMessage}");
                }
            }
        }
    }

    // Вызывается каждый тик для поддержки HTML center
    public void OnTick()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            var user = _users[player.Slot];
            if (user == null) continue;

            if (user.HtmlPrint)
            {
                var showWhenDead = _config.ShowHtmlWhenDead ?? false;
                if (!showWhenDead && !player.PawnIsAlive)
                    continue;

                var duration = _config.HtmlCenterDuration;
                if (duration != null && TimeSpan.FromSeconds(user.PrintTime / 64.0).TotalSeconds < duration.Value)
                {
                    player.PrintToCenterHtml(user.Message);
                    user.PrintTime++;
                }
                else
                {
                    user.HtmlPrint = false;
                }
            }
        }
    }

    public void ClearUsers()
    {
        for (int i = 0; i < _users.Length; i++) _users[i] = null;
    }

    private void SetHtmlPrintSettings(CCSPlayerController player, string message)
    {
        var user = _users[player.Slot];
        if (user == null)
        {
            _users[player.Slot] = new User();
            user = _users[player.Slot];
        }

        user!.HtmlPrint = true;
        user.PrintTime = 0;
        user.Message = message;
    }
}
