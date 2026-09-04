using System;
using System.Globalization;

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
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        Console.WriteLine($"[{timestamp}] [NotifyMessages] [INFO] {message}");
    }

    public void Debug(string message)
    {
        if (_isDebug())
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Console.WriteLine($"[{timestamp}] [NotifyMessages] [DEBUG] {message}");
        }
    }

    public void Error(string message, Exception? ex = null)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (ex != null)
            Console.WriteLine($"[{timestamp}] [NotifyMessages] [ERROR] {message} => {ex.Message}\nStack trace: {ex.StackTrace}");
        else
            Console.WriteLine($"[{timestamp}] [NotifyMessages] [ERROR] {message}");
    }
}
