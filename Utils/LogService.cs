namespace NotifyMessages;

/// Глобальный доступ к логгеру плагина для утилит/статических хелперов
public static class LogService
{
    /// Текущий логгер плагина. Задаётся в Load() основного плагина.
    public static ILogger? Current { get; set; }

    public static void Info(string message) => Current?.Info(message);
    public static void Debug(string message) => Current?.Debug(message);
    public static void Error(string message, System.Exception? ex = null) => Current?.Error(message, ex);
}
