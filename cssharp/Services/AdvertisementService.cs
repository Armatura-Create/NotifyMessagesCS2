using System;
using System.Collections.Generic;

namespace NotifyMessages;

/// Сервис показа рекламы: управление таймерами и вывод сообщений.
///
/// Фреймворка здесь нет: таймер и рассылка приходят делегатами из плагина,
/// поэтому класс одинаков в cssharp/ и swiftly/.
public sealed class AdvertisementService
{
    private readonly Config _config;
    private readonly ILogger _logger;

    /// (интервал в секундах, действие) -> как остановить
    private readonly Func<float, Action, Action> _repeatEvery;

    /// Рассылка всем игрокам в указанный канал
    private readonly Action<MessageType, string> _broadcast;

    private readonly List<Action> _stopTimers = new();

    public AdvertisementService(
        Config config,
        ILogger logger,
        Func<float, Action, Action> repeatEvery,
        Action<MessageType, string> broadcast)
    {
        _config = config;
        _logger = logger;
        _repeatEvery = repeatEvery;
        _broadcast = broadcast;
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
            _stopTimers.Add(_repeatEvery(Math.Max(1f, ad.Interval), () => ShowAd(ad)));
        }
    }

    public void Stop()
    {
        foreach (var stop in _stopTimers) stop();
        _stopTimers.Clear();
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
            _broadcast(channel, message);
        }
    }
}
