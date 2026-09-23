#include "core/display_service.h"

#include "core/logger.h"
#include "core/players.h"
#include "core/text_formatter.h"

#include <map>

namespace nm {

// Settings.PrintToCenterHtml — совместимость со старыми конфигами: поднимает
// обычный центр до HTML. Решение принимается ДО рендера, иначе текст был бы
// отрисован как plain, а отправлен в HTML-панель.
MessageType DisplayService::ResolveChannel(MessageType messageType) const {
    MessageType channel = messageType;
    if (channel == MessageType::Center && _config->printToCenterHtml.value_or(false)) {
        channel = MessageType::CenterHtml;
    }
    if (channel == MessageType::CenterHtml && !_sink->HtmlAvailable()) channel = MessageType::Center;
    return channel;
}

void DisplayService::Print(MessageType messageType, const std::string& message, int targetSlot,
                           const Values* values) {
    if (message.empty()) return;

    const MessageType channel = ResolveChannel(messageType);
    const char* channelName = MessageTypeName(channel);

    if (targetSlot >= 0) {
        const PlayerInfo* target = _players->Find(targetSlot);
        if (target == nullptr || !target->inGame || target->fake) return;

        const std::string processed = _processor->ProcessMessage(message, target->steamId, channel, values);
        SendTo(target->slot, target->steamId, channel, processed);

        if (_config->debug) {
            std::string iso = _processor->LanguageOf(target->steamId);
            if (iso.empty()) iso = _config->defaultLang.empty() ? "default" : _config->defaultLang;
            _logger->Debug(std::string("[") + channelName + "] -> player '" + target->name + "' (" + iso +
                           "): " + text::StripColorCodes(processed));
        }
        return;
    }

    // Кеш по языку: обработка один раз на язык, а не на каждого игрока
    std::map<std::string, std::string> processedByLanguage;
    int playerCount = 0;

    for (const PlayerInfo& player : _players->Humans()) {
        ++playerCount;

        std::string iso = _processor->LanguageOf(player.steamId);
        if (iso.empty()) iso = _config->defaultLang.empty() ? "default" : _config->defaultLang;

        auto cached = processedByLanguage.find(iso);
        if (cached == processedByLanguage.end()) {
            cached = processedByLanguage
                         .emplace(iso, _processor->ProcessMessage(message, player.steamId, channel, values))
                         .first;
        }
        SendTo(player.slot, player.steamId, channel, cached->second);
    }

    if (!_config->debug) return;

    if (playerCount > 0) {
        _logger->Debug(std::string("[") + channelName + "] -> " + std::to_string(playerCount) + " player(s), " +
                       std::to_string(processedByLanguage.size()) + " language(s):");
        for (const auto& entry : processedByLanguage) {
            _logger->Debug("  [" + entry.first + "] " + text::StripColorCodes(entry.second));
        }
    } else {
        // Игроков нет — как сообщение выглядело бы на языке по умолчанию
        const std::string processed = _processor->ProcessMessage(message, 0, channel, values);
        _logger->Debug(std::string("[") + channelName + "] -> No valid players online. Message [" +
                       (_config->defaultLang.empty() ? "default" : _config->defaultLang) +
                       "]: " + text::StripColorCodes(processed));
    }
}

void DisplayService::SendTo(int slot, uint64_t steamId, MessageType channel, const std::string& processed) {
    switch (channel) {
        case MessageType::Chat:
            _sink->Chat(slot, text::EnsureChatColorPrefix(processed));
            break;
        case MessageType::Console:
            _sink->Console(slot, processed);
            break;
        case MessageType::Alert:
            _sink->Alert(slot, processed);
            break;
        case MessageType::CenterHtml: {
            if (slot < 0 || slot >= kMaxSlots) {
                _logger->Error("[DisplayService] Player slot " + std::to_string(slot) + " out of range (" +
                               std::to_string(kMaxSlots) + "), HTML center skipped");
                return;
            }
            User& user = _users[slot];
            // Счётчик — лишь повод OnTick не выйти сразу, точное число он пересчитает
            // сам. Условное «++, если слот не был активен» теряло сообщение: слот
            // ушедшего игрока остаётся htmlPrint, и новому владельцу счётчик не рос.
            ++_htmlActiveCount;
            user.htmlPrint = true;
            user.shownSince = -1;
            user.message = processed;
            user.steamId = steamId;
            break;
        }
        default:
            _sink->Center(slot, processed);
            break;
    }
}

// Обход — по реестру игроков, а не по массиву слотов: слот игрока, отвалившегося
// без события disconnect, иначе залипал бы навсегда. Счётчик пересчитывается по
// факту в конце прохода, а не ведётся вручную — по той же причине.
//
// Settings.ShowHtmlWhenDead в этой цели не действует: пауза на время смерти
// требует читать поле пешки, то есть смещения движка, которых здесь нет.
void DisplayService::OnTick(double now) {
    if (_htmlActiveCount <= 0) return;

    const double duration = _config->htmlCenterDuration.value_or(kDefaultHtmlDurationSeconds);
    int stillActive = 0;

    for (const PlayerInfo& player : _players->Humans()) {
        if (player.slot < 0 || player.slot >= kMaxSlots) continue;

        User& user = _users[player.slot];
        if (!user.htmlPrint) continue;

        if (user.shownSince < 0) user.shownSince = now;
        if (ShouldStopShowing(user.steamId, player.steamId, now - user.shownSince, duration)) {
            user.htmlPrint = false;
            continue;
        }

        _sink->CenterHtml(player.slot, user.message);
        ++stillActive;
    }

    _htmlActiveCount = stillActive;
}

void DisplayService::ClearUsers() {
    for (User& user : _users) user = User();
    _htmlActiveCount = 0;
}

}  // namespace nm
