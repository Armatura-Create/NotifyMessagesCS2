using System;
using Microsoft.Extensions.Logging;

namespace NotifyMessages;

/// Логгер плагина поверх Core.Logger, уважающий Debug-флаг конфига.
///
/// Пишем через логгер SwiftlyS2, а не в Console: у фреймворка свои синки (консоль,
/// файл), и собственный Console.WriteLine прошёл бы мимо них и потерял метку плагина.
///
/// Флаг читается через замыкание, поэтому подхватывается после sw_reload_advert.
///
/// Сообщения объявлены через [LoggerMessage]: генератор разворачивает их в
/// закешированные делегаты, и анализаторы (CA1848/CA1873) видят, что аргументы
/// не вычисляются, когда уровень выключен.
public sealed partial class PluginLogger : ILogger
{
    private readonly Microsoft.Extensions.Logging.ILogger _sink;
    private readonly Func<bool> _isDebug;

    public PluginLogger(Microsoft.Extensions.Logging.ILogger sink, Func<bool> isDebug)
    {
        _sink = sink;
        _isDebug = isDebug;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "[NotifyMessages] {Message}")]
    private static partial void WriteInfo(Microsoft.Extensions.Logging.ILogger logger, string message);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "[NotifyMessages] [DEBUG] {Message}")]
    private static partial void WriteDebug(Microsoft.Extensions.Logging.ILogger logger, string message);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "[NotifyMessages] {Message} => {Error}")]
    private static partial void WriteFailure(Microsoft.Extensions.Logging.ILogger logger, string message, string error);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "[NotifyMessages] {Message}")]
    private static partial void WriteFailure(Microsoft.Extensions.Logging.ILogger logger, string message);

    public void Info(string message) => WriteInfo(_sink, message);

    public void Debug(string message)
    {
        // Debug печатает SteamID, ники и гео игроков — по умолчанию выключен.
        // Собственный флаг, а не уровень логгера хоста: настройка плагина не должна
        // зависеть от того, как настроено логирование сервера.
        if (_isDebug()) WriteDebug(_sink, message);
    }

    public void Error(string message, Exception? ex = null)
    {
        // Стек кладём в тот же аргумент: отдельный параметр Exception логгер напечатал бы
        // своим форматом, разорвав строку, по которой ищут в логе.
        if (ex != null)
            WriteFailure(_sink, message, ex.Message + "\nStack trace: " + ex.StackTrace);
        else
            WriteFailure(_sink, message);
    }
}
