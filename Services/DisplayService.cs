using System;
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
    }

    public void Update(Config config, MessageProcessor messageProcessor)
    {
        // Обновление конфигурации и процессора при перезагрузке
        // Состояние пользователей сохраняем
        _config = config;
        _messageProcessor = messageProcessor;
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
        }
        else
        {
            foreach (var player in Utilities.GetPlayers().Where(u => !privateMsg && !u.IsBot && u.IsValid))
            {
                var processed = _messageProcessor.ProcessMessage(message, player.SteamID);

                switch (destination)
                {
                    case HudDestination.Chat:
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
        }

        if (!_config.Debug) return;
        {
            var processed = _messageProcessor.ProcessMessage(message, 0);
            _logger.Debug("[ADS DEBUG] " + TextFormatter.StripColorCodes(processed));
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
                if (duration != null && TimeSpan.FromSeconds(user.PrintTime / 64.0).Seconds < duration.Value)
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

        user.HtmlPrint = true;
        user.PrintTime = 0;
        user.Message = message;
    }
}
