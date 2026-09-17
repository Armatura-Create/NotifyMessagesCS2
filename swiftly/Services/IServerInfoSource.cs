namespace NotifyMessages;

/// Факты о сервере для системных тегов {MAP} {SERVERNAME} {IP} {PORT} {MAXPLAYERS} {PLAYERS}.
///
/// Реализует плагин — только он умеет читать движок. Сервисы получают интерфейс и потому
/// не знают, под каким фреймворком работают: один и тот же MessageProcessor живёт
/// и в cssharp/, и в swiftly/, и проверяется тестами без сервера.
///
/// Свойства читаются ЛЕНИВО — из событий, команд и таймеров, где движок уже готов.
/// В Load() и конструкторах их не трогать: нативы там ещё недоступны.
public interface IServerInfoSource
{
    string MapName { get; }
    string Hostname { get; }
    string Ip { get; }
    string Port { get; }
    int MaxPlayers { get; }
    int Players { get; }
}
