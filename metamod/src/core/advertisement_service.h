// Показ рекламы: таймер на каждый блок Ads.json, сообщения блока по кругу.
// Порт Services/AdvertisementService.cs — таймер и рассылка приходят делегатами.
#pragma once

#include "core/config.h"

#include <functional>
#include <string>
#include <vector>

namespace nm {

class ILogger;

// (интервал в секундах, действие) -> как остановить
using RepeatEvery = std::function<std::function<void()>(float, std::function<void()>)>;
// Рассылка всем игрокам в канал
using Broadcast = std::function<void(MessageType, const std::string&)>;

class AdvertisementService {
public:
    // Блоки копируются: ротация живёт в сервисе, и перезагрузка конфига не оставит
    // таймерам висячих указателей на старые блоки
    AdvertisementService(const std::vector<Advertisement>& ads, ILogger* logger, RepeatEvery repeatEvery,
                         Broadcast broadcast);
    ~AdvertisementService() { Stop(); }

    AdvertisementService(const AdvertisementService&) = delete;
    AdvertisementService& operator=(const AdvertisementService&) = delete;

    void Start();
    void Stop();

private:
    void ShowAd(size_t index);

    std::vector<Advertisement> _ads;
    ILogger* _logger;
    RepeatEvery _repeatEvery;
    Broadcast _broadcast;
    std::vector<std::function<void()>> _stopTimers;
};

}  // namespace nm
