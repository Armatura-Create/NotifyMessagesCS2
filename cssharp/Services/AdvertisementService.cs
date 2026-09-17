using System;
using System.Collections.Generic;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace NotifyMessages;

/// Сервис показа рекламы: управление таймерами и вывод сообщений
public sealed class AdvertisementService
{
    private readonly Config _config;
    private readonly ILogger _logger;
    private readonly Func<float, Action, TimerFlags, Timer> _addTimer;
    private readonly Action<MessageType, string, CCSPlayerController?> _print;

    private readonly List<Timer> _timers = new();

    public AdvertisementService(
        Config config,
        ILogger logger,
        Func<float, Action, TimerFlags, Timer> addTimer,
        Action<MessageType, string, CCSPlayerController?> print)
    {
        _config = config;
        _logger = logger;
        _addTimer = addTimer;
        _print = print;
    }

    public void Start()
    {
        if (_config.Ads == null || _config.Ads.Count == 0)
            return;

        for (var i = 0; i < _config.Ads.Count; i++)
        {
            var ad = _config.Ads[i];

            if (ad.Messages == null || ad.Messages.Count == 0)
            {
                _logger.Info($"[ADS] Block #{i + 1} has no messages, skipped");
                continue;
            }

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
        if (messages == null) return;

        foreach (var (type, message) in messages)
        {
            // Одна нотация канала на весь конфиг. Раньше здесь был switch по точному регистру:
            // "chat" молча терялся, а Alert и CenterHtml не поддерживались вовсе.
            if (!Enum.TryParse<MessageType>(type, ignoreCase: true, out var channel))
            {
                // Это ошибка конфига, а не отладочный шум — админ должен её увидеть
                _logger.Info($"[ADS] Неизвестный канал '{type}'. Допустимые: " +
                             "Chat, Center, CenterHtml, Console, Alert");
                continue;
            }

            // Делегируем обработку/локализацию и фактический вывод наружу
            _print(channel, message, null);
        }
    }
}
