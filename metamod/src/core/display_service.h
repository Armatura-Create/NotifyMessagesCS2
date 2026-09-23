// Доставка сообщений игрокам и HTML-центр. Порт Services/DisplayService.cs.
//
// В C#-целях этот сервис стоит на фреймворке. Здесь фреймворка нет, и вся логика —
// выбор канала, кеш рассылки по языку, жизнь HTML-сообщения в слоте — живёт в ядре
// и проверяется тестами. К движку ведёт один узкий интерфейс IMessageSink.
#pragma once

#include "core/message_processor.h"

#include <cstdint>
#include <string>

namespace nm {

class ILogger;
class PlayerRegistry;

// Доставка одной готовой строки одному игроку. Единственная реализация — в
// mm/output.cpp (UserMessage TextMsg, ClientPrintf, событие HTML-панели); в тестах —
// запись в память. Интерфейс ради тестов, как ILogger.
class IMessageSink {
public:
    virtual ~IMessageSink() = default;

    virtual void Chat(int slot, const std::string& text) = 0;
    virtual void Center(int slot, const std::string& text) = 0;
    virtual void Alert(int slot, const std::string& text) = 0;
    virtual void Console(int slot, const std::string& text) = 0;
    virtual void CenterHtml(int slot, const std::string& html) = 0;

    // HTML-панель требует менеджера игровых событий. Пока его нет, CenterHtml
    // выводится обычным центром — лучше без цветов, чем никак.
    virtual bool HtmlAvailable() const = 0;
};

class DisplayService {
public:
    // Слотов с запасом: CS2 держит до 64 игроков. Константа, а не maxClients
    // движка: сервис создаётся в Load(), где globals ещё может не быть.
    static constexpr int kMaxSlots = 128;
    static constexpr float kDefaultHtmlDurationSeconds = 5.0f;

    DisplayService(const Config* config, const MessageProcessor* processor, const PlayerRegistry* players,
                   IMessageSink* sink, ILogger* logger)
        : _config(config), _processor(processor), _players(players), _sink(sink), _logger(logger) {}

    // targetSlot < 0 — всем, иначе только этому игроку. values подставляются
    // внутри ProcessMessage до рендера. Рассылка обрабатывает шаблон один раз
    // на язык, а не на игрока.
    void Print(MessageType messageType, const std::string& message, int targetSlot = -1,
               const Values* values = nullptr);

    // Каждый кадр. HTML-панель держится, только пока её перерисовывают.
    void OnTick(double now);

    void ClearUsers();

    // Пора ли гасить: истекло время или слот уже достался другому игроку
    static bool ShouldStopShowing(uint64_t ownerSteamId, uint64_t currentSteamId, double elapsedSeconds,
                                  double durationSeconds) {
        return ownerSteamId != currentSteamId || elapsedSeconds >= durationSeconds;
    }

private:
    struct User {
        bool htmlPrint = false;
        std::string message;
        double shownSince = -1;  // < 0 — ещё ни разу не нарисовано
        uint64_t steamId = 0;    // владелец: слот переиспользуется движком
    };

    MessageType ResolveChannel(MessageType messageType) const;
    void SendTo(int slot, uint64_t steamId, MessageType channel, const std::string& processed);

    const Config* _config;
    const MessageProcessor* _processor;
    const PlayerRegistry* _players;
    IMessageSink* _sink;
    ILogger* _logger;

    User _users[kMaxSlots];
    // Сколько слотов реально ждут перерисовки. Пока 0 — OnTick выходит сразу.
    int _htmlActiveCount = 0;
};

}  // namespace nm
