#include "core/advertisement_service.h"

#include "core/logger.h"

#include <utility>

namespace nm {

AdvertisementService::AdvertisementService(const std::vector<Advertisement>& ads, ILogger* logger,
                                           RepeatEvery repeatEvery, Broadcast broadcast)
    : _ads(ads), _logger(logger), _repeatEvery(std::move(repeatEvery)), _broadcast(std::move(broadcast)) {}

void AdvertisementService::Start() {
    for (size_t i = 0; i < _ads.size(); ++i) {
        if (_ads[i].messages.empty()) {
            _logger->Info("[ADS] Block #" + std::to_string(i + 1) + " has no messages, skipped");
            continue;
        }

        const float interval = _ads[i].interval > 1.0f ? _ads[i].interval : 1.0f;
        _stopTimers.push_back(_repeatEvery(interval, [this, i]() { ShowAd(i); }));
    }
}

void AdvertisementService::Stop() {
    for (auto& stop : _stopTimers) {
        if (stop) stop();
    }
    _stopTimers.clear();
}

void AdvertisementService::ShowAd(size_t index) {
    const OrderedPairs* messages = _ads[index].NextMessages();
    if (messages == nullptr) return;

    for (const auto& message : *messages) {
        // Одна нотация канала на весь конфиг: "chat" и "Chat" — одно и то же
        MessageType channel = MessageType::Chat;
        if (!ParseMessageType(message.first, &channel)) {
            // Это ошибка конфига, а не отладочный шум — админ должен её увидеть
            _logger->Info("[ADS] Неизвестный канал '" + message.first +
                          "'. Допустимые: Chat, Center, CenterHtml, Console, Alert");
            continue;
        }
        _broadcast(channel, message.second);
    }
}

}  // namespace nm
