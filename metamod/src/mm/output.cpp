#include "mm/output.h"

#include "mm/globals.h"

#include <eiface.h>
#include <engine/igameeventsystem.h>
#include <igameevents.h>
#include <irecipientfilter.h>
#include <networksystem/inetworkmessages.h>
#include <networksystem/inetworkserializer.h>
// CNetMessagePB и ToPB<>: inetworkserializer.h объявляет CNetMessage только вперёд
#include <networksystem/netmessage.h>

#include "gameevents.pb.h"
#include "usermessages.pb.h"

namespace nm {
namespace {

// Назначения TextMsg (shareddefs.h: HUD_PRINTTALK / HUD_PRINTCENTER / HUD_PRINTALERT)
constexpr int kHudChat = 3;
constexpr int kHudCenter = 4;
constexpr int kHudAlert = 6;

// Сколько секунд живёт кадр HTML-панели у клиента. Панель перерисовывается каждый
// тик (DisplayService::OnTick), как PrintToCenterHtml(message, 5) в CSSharp.
constexpr int kHtmlFrameSeconds = 5;

// Получатель ровно один. Свой фильтр, а не маска uint64 у PostEventAbstract:
// комментарий к ней в SDK говорит «client index - 1», а плагины ставят бит по
// самому слоту — угадывать по противоречивой документации незачем.
class SingleRecipientFilter final : public IRecipientFilter {
public:
    explicit SingleRecipientFilter(int slot) {
        _recipients.ClearAll();
        _recipients.Set(slot);
    }

    NetChannelBufType_t GetNetworkBufType() const override { return BUF_RELIABLE; }
    bool IsInitMessage() const override { return false; }
    const CPlayerBitVec& GetRecipients() const override { return _recipients; }
    CPlayerSlot GetPredictedPlayerSlot() const override { return -1; }

private:
    CPlayerBitVec _recipients;
};

bool ValidSlot(int slot) { return slot >= 0 && slot < ABSOLUTE_PLAYER_LIMIT; }

// Тип сообщения живёт столько же, сколько процесс, — ищется один раз.
// Статики функций зовутся только из главного потока.
INetworkMessageInternal* TextMsgType() {
    static INetworkMessageInternal* type = nullptr;
    if (type == nullptr && g_networkMessages != nullptr) {
        type = g_networkMessages->FindNetworkMessagePartial("TextMsg");
    }
    return type;
}

INetworkMessageInternal* LegacyEventType() {
    static INetworkMessageInternal* type = nullptr;
    if (type == nullptr && g_networkMessages != nullptr) {
        type = g_networkMessages->FindNetworkMessageById(GE_Source1LegacyGameEvent);
    }
    return type;
}

void SendTextMsg(int slot, int destination, const std::string& text) {
    if (!ValidSlot(slot) || g_gameEventSystem == nullptr) return;

    INetworkMessageInternal* type = TextMsgType();
    if (type == nullptr) return;

    // Сообщение создаёт движок, а не мы: CNetMessagePB требует привязки к
    // протобуфу, идентификатора и группы, которые руками не собрать
    CNetMessagePB<CUserMessageTextMsg>* message = type->AllocateMessage()->ToPB<CUserMessageTextMsg>();
    message->set_dest(destination);
    message->add_param(text);

    SingleRecipientFilter filter(slot);
    g_gameEventSystem->PostEventAbstract(-1, false, &filter, type, message, 0);
    delete message;
}

}  // namespace

void EngineOutput::Chat(int slot, const std::string& text) { SendTextMsg(slot, kHudChat, text); }

void EngineOutput::Center(int slot, const std::string& text) { SendTextMsg(slot, kHudCenter, text); }

void EngineOutput::Alert(int slot, const std::string& text) { SendTextMsg(slot, kHudAlert, text); }

void EngineOutput::Console(int slot, const std::string& text) {
    if (!ValidSlot(slot) || g_engine == nullptr) return;
    g_engine->ClientPrintf(CPlayerSlot(slot), (text + "\n").c_str());
}

bool EngineOutput::HtmlAvailable() const { return g_gameEventManager != nullptr && LegacyEventType() != nullptr; }

void EngineOutput::CenterHtml(int slot, const std::string& html) {
    if (!ValidSlot(slot) || g_gameEventSystem == nullptr || !HtmlAvailable()) return;

    // Событие создаётся на каждый кадр и тут же освобождается, а не кешируется:
    // описания событий движок перечитывает при смене карты, и закешированный
    // объект мог бы пережить своё описание
    IGameEvent* event = g_gameEventManager->CreateEvent("show_survival_respawn_status", true);
    if (event == nullptr) return;

    event->SetString("loc_token", html.c_str());
    event->SetInt("duration", kHtmlFrameSeconds);
    event->SetInt("userid", slot);

    INetworkMessageInternal* type = LegacyEventType();
    CNetMessagePB<CMsgSource1LegacyGameEvent>* message =
        type->AllocateMessage()->ToPB<CMsgSource1LegacyGameEvent>();

    if (g_gameEventManager->SerializeEvent(event, message)) {
        SingleRecipientFilter filter(slot);
        g_gameEventSystem->PostEventAbstract(-1, false, &filter, type, message, 0);
    }

    delete message;
    g_gameEventManager->FreeEvent(event);
}

}  // namespace nm
