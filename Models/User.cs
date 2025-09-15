namespace NotifyMessages;

// Класс для хранения текущего состояния игрока
public class User
{
    public bool HtmlPrint { get; set; }
    public string Message { get; set; } = string.Empty;
    public int PrintTime { get; set; }
}
