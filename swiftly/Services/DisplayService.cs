using System;
using System.Collections.Generic;
using SwiftlyS2.Shared.Players;

namespace NotifyMessages;

/// Сервис вывода сообщений.
///
/// В отличие от версии для CounterStrikeSharp здесь нет ни слотов, ни OnTick:
/// SwiftlyS2 сам держит HTML-панель на экране столько миллисекунд, сколько передано
/// в SendCenterHTML. Поэтому Settings.ShowHtmlWhenDead в этой цели не действует —
/// пауза на время смерти игрока требует перерисовки тиками, которой здесь нет.
public sealed class DisplayService
{
    // Если HtmlCenterDuration не задан в конфиге — показываем столько секунд.
    private const float DefaultHtmlDurationSeconds = 5f;

    private Config _config;
    private MessageProcessor _messageProcessor;
    private readonly ILogger _logger;

    /// Все игроки на сервере (Core.PlayerManager.GetAllPlayers). Делегат, а не Core:
    /// сервис не должен знать больше, чем ему нужно.
    private readonly Func<IEnumerable<IPlayer>> _players;

    public DisplayService(Config config, MessageProcessor messageProcessor, ILogger logger,
        Func<IEnumerable<IPlayer>> players)
    {
        _config = config;
        _messageProcessor = messageProcessor;
        _logger = logger;
        _players = players;

        _logger.Debug($"[DisplayService] Initialized with Debug={_config.Debug}");
    }

    public void Update(Config config, MessageProcessor messageProcessor)
    {
        _config = config;
        _messageProcessor = messageProcessor;

        _logger.Debug($"[DisplayService] Config updated, Debug={_config.Debug}");
    }

    /// Унифицированный вывод строки с локализацией, подстановкой значений и рендером под канал.
    /// target == null — сообщение всем игрокам, иначе только указанному.
    /// values — контекстные значения ({PLAYERNAME}, {TEAM}, {SECONDS}, ...), подставляются
    /// внутри ProcessMessage до рендера.
    public void Print(MessageType messageType, string message, IPlayer? target = null,
        IReadOnlyDictionary<string, string>? values = null)
    {
        if (string.IsNullOrEmpty(message)) return;

        var channel = ResolveChannel(messageType);

        if (target != null)
        {
            if (!IsHuman(target)) return;

            var processed = _messageProcessor.ProcessMessage(message, target.SteamID, channel, values);
            SendTo(target, channel, processed);

            if (_config.Debug)
            {
                var isoCode = _messageProcessor.GetIsoCodeBySteamId(target.SteamID) ?? _config.DefaultLang ?? "default";
                _logger.Debug(
                    $"[{channel}] -> player '{SafeName(target)}' ({isoCode}): {TextFormatter.StripColorCodes(processed)}");
            }

            return;
        }

        // Кешируем обработанные сообщения по ISO-коду: обработка идёт один раз на язык,
        // а не на каждого игрока.
        var processedMessages = new Dictionary<string, string>();
        var playerCount = 0;

        foreach (var player in _players())
        {
            if (!IsHuman(player)) continue;

            playerCount++;

            var isoCode = _messageProcessor.GetIsoCodeBySteamId(player.SteamID) ?? _config.DefaultLang ?? "default";

            if (!processedMessages.TryGetValue(isoCode, out var processed))
            {
                processed = _messageProcessor.ProcessMessage(message, player.SteamID, channel, values);
                processedMessages[isoCode] = processed;
            }

            SendTo(player, channel, processed);
        }

        if (!_config.Debug) return;

        if (playerCount > 0)
        {
            _logger.Debug($"[{channel}] -> {playerCount} player(s), {processedMessages.Count} language(s):");
            foreach (var (isoCode, processed) in processedMessages)
                _logger.Debug($"  [{isoCode}] {TextFormatter.StripColorCodes(processed)}");
        }
        else
        {
            var processed = _messageProcessor.ProcessMessage(message, 0, channel, values);
            var defaultLang = _config.DefaultLang ?? "default";
            _logger.Debug(
                $"[{channel}] -> No valid players online. Message [{defaultLang}]: {TextFormatter.StripColorCodes(processed)}");
        }
    }

    /// Глобальный Settings.PrintToCenterHtml оставлен для совместимости со старыми конфигами:
    /// он поднимает обычный центр до HTML. Решение принимается ДО рендера — иначе текст
    /// был бы отрисован как plain, а отправлен в HTML-панель.
    private MessageType ResolveChannel(MessageType messageType)
        => messageType == MessageType.Center && _config.PrintToCenterHtml == true
            ? MessageType.CenterHtml
            : messageType;

    private int HtmlDurationMs
        => (int)Math.Max(100f, (_config.HtmlCenterDuration ?? DefaultHtmlDurationSeconds) * 1000f);

    private static bool IsHuman(IPlayer player)
    {
        try
        {
            return player.IsValid && !player.IsFakeClient;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string SafeName(IPlayer player)
    {
        try
        {
            return player.Name;
        }
        catch (Exception)
        {
            return "?";
        }
    }

    /// Отправка одному игроку. Send* бросают, если сущность стала невалидной между
    /// проверкой и вызовом (игрок вышел в этом же кадре). Ловим точечно: пропустить
    /// одного получателя дешевле, чем сорвать рассылку.
    private void SendTo(IPlayer player, MessageType channel, string processed)
    {
        try
        {
            SendToCore(player, channel, processed);
        }
        catch (Exception ex)
        {
            _logger.Debug($"[DisplayService] Не удалось отправить сообщение: {ex.Message}");
        }
    }

    private void SendToCore(IPlayer player, MessageType channel, string processed)
    {
        switch (channel)
        {
            case MessageType.Chat:
                player.SendChat(TextFormatter.EnsureChatColorPrefix(processed));
                break;
            case MessageType.Console:
                player.SendConsole(processed);
                break;
            case MessageType.Alert:
                player.SendAlert(processed);
                break;
            case MessageType.CenterHtml:
                player.SendCenterHTML(processed, HtmlDurationMs);
                break;
            default:
                player.SendCenter(processed);
                break;
        }
    }
}
