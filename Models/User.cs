namespace NotifyMessages;

// Класс для хранения текущего состояния игрока
public class User
{
    public bool HtmlPrint { get; set; }
    public string Message { get; set; } = string.Empty;
    public int PrintTime { get; set; }

    /// Кому адресовано сообщение. Слот переиспользуется движком, и без этой сверки
    /// новый игрок увидел бы HTML-сообщение предыдущего владельца слота.
    public ulong SteamId { get; set; }
}
