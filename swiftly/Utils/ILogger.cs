namespace NotifyMessages;

/// Простой интерфейс логгера для сервисов
public interface ILogger
{
    void Info(string message);
    void Debug(string message);
    void Error(string message, System.Exception? ex = null);
}
