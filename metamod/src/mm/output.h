// Доставка готовой строки игроку: единственное место цели, которое знает про
// UserMessage и протобуфы SDK.
//
// Chat / Center / Alert — UserMessage TextMsg с HUD-назначением (3 / 4 / 6): ровно
// это сообщение собирает UTIL_ClientPrint игры, которым пользуется ClientPrint
// в CounterStrikeSharp. Тип сообщения отдаёт INetworkMessages, доставку делает
// IGameEventSystem — оба фабричные, ни сигнатур, ни смещений.
// Console — IVEngineServer2::ClientPrintf.
// CenterHtml — событие show_survival_respawn_status, сериализованное в
// CMsgSource1LegacyGameEvent и отправленное одному получателю (как CS2Fixes).
// Нужен менеджер игровых событий; пока его нет, HtmlAvailable() == false.
#pragma once

#include "core/display_service.h"

namespace nm {

class EngineOutput final : public IMessageSink {
public:
    void Chat(int slot, const std::string& text) override;
    void Center(int slot, const std::string& text) override;
    void Alert(int slot, const std::string& text) override;
    void Console(int slot, const std::string& text) override;
    void CenterHtml(int slot, const std::string& html) override;
    bool HtmlAvailable() const override;
};

}  // namespace nm
