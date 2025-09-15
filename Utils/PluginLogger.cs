using System;

namespace NotifyMessages;

/// Логгер плагина, уважающий Debug-флаг конфига
public sealed class PluginLogger : ILogger
{
    private readonly Func<bool> _isDebug;

    public PluginLogger(Func<bool> isDebug)
    {
        _isDebug = isDebug;
    }

    public void Info(string message)
    {
        Console.WriteLine($"[NotifyMessages] {message}");
    }

    public void Debug(string message)
    {
        if (_isDebug())
            Console.WriteLine($"[NotifyMessages][DEBUG] {message}");
    }

    public void Error(string message, Exception? ex = null)
    {
        if (ex != null)
            Console.WriteLine($"[NotifyMessages][ERROR] {message} => {ex.Message}");
        else
            Console.WriteLine($"[NotifyMessages][ERROR] {message}");
    }
}
