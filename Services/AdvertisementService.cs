using System;
using System.Collections.Generic;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace NotifyMessages;

/// Сервис показа рекламы: управление таймерами и вывод сообщений
public sealed class AdvertisementService
{
    private Config _config;
    private MessageProcessor _messageProcessor; // зарезервировано на будущее, сейчас обработка в PrintWrappedLine
    private readonly ILogger _logger;
    private readonly Func<float, Action, TimerFlags, Timer> _addTimer;
    private readonly Action<HudDestination?, string, CCSPlayerController?, bool> _print;

    private readonly List<Timer> _timers = new();

    public AdvertisementService(
        Config config,
        MessageProcessor messageProcessor,
        ILogger logger,
        Func<float, Action, TimerFlags, Timer> addTimer,
        Action<HudDestination?, string, CCSPlayerController?, bool> print)
    {
        _config = config;
        _messageProcessor = messageProcessor;
        _logger = logger;
        _addTimer = addTimer;
        _print = print;
    }

    /// Обновить ссылки на конфиг/процессор без пересоздания сервиса
    public void Update(Config config, MessageProcessor messageProcessor)
    {
        _config = config;
        _messageProcessor = messageProcessor;
    }

    public void Start()
    {
        if (_config.Ads == null || _config.Ads.Count == 0)
            return;

        foreach (var ad in _config.Ads)
        {
            // Запускаем цикл показа конкретного блока рекламы
            _timers.Add(_addTimer(Math.Max(1f, ad.Interval), () => ShowAd(ad), TimerFlags.REPEAT));
        }
    }

    public void Stop()
    {
        foreach (var t in _timers) t.Kill();
        _timers.Clear();
    }

    private void ShowAd(Advertisement ad)
    {
        var messages = ad.NextMessages;
        foreach (var (type, message) in messages)
        {
            HudDestination? dest = null;
            switch (type)
            {
                case "Chat":
                    dest = HudDestination.Chat; break;
                case "Center":
                    dest = HudDestination.Center; break;
                case "Console":
                    dest = HudDestination.Console; break;
                default:
                    // неизвестный тип — пропускаем
                    _logger.Debug($"[ADS] Unknown message type '{type}'");
                    continue;
            }

            // Делегируем обработку/локализацию и фактический вывод наружу
            _print(dest, message, null, false);
        }
    }
}
