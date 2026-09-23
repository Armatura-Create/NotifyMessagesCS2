// Плагин Metamod:Source.
//
// Здесь и только здесь живут обращения к движку. Логика — в src/core, которое про
// SDK ничего не знает и проверяется тестами на любой машине.
//
// Раскладка повторяет cssharp/ и swiftly/, насколько позволяет язык:
//   plugin.cpp    — Load/Unload, сборка сервисов, фабрики таймеров   (NotifyMessages.cs)
//   events.cpp    — вход, выход, смена команды                       (Events/*.cs)
//   commands.cpp  — mm_servers, mm_restart_notify, mm_reload_advert,
//                   mm_nm_check, mm_nm_preview, чат !servers          (Commands/*.cs)
//   output.cpp    — доставка в чат/центр/HTML/консоль/alert          (DisplayService.cs, половина с движком)
//   rtti.cpp      — поиск CGameEventManager
//
// Сигнатур и смещений в плагине НЕТ. Игроки — из хуков IServerGameClients
// (SteamID, ник и IP отдаёт сам движок), вывод — через фабричные интерфейсы,
// менеджер событий — по RTTI-имени класса. Поэтому обновление CS2 его не ломает;
// сломать может только смена раскладки vtable в SDK, и это ловит сборка против
// свежего hl2sdk.
#pragma once

#include "core/advertisement_service.h"
#include "core/config.h"
#include "core/display_service.h"
#include "core/geoip.h"
#include "core/language.h"
#include "core/logger.h"
#include "core/message_processor.h"
#include "core/players.h"
#include "core/scheduler.h"
#include "core/server_status_service.h"
#include "core/session_service.h"
#include "mm/output.h"

#include <cstdint>
#include <ISmmPlugin.h>
// Полные типы нужны KHook::Virtual: индекс в vtable считается из указателя на метод
#include <eiface.h>
#include <icvar.h>
#include <igameevents.h>
// uint64 SDK — unsigned long long, а uint64_t на Linux — unsigned long. Сигнатуры
// хуков обязаны повторять SDK дословно.
#include <tier0/platform.h>
#include <functional>
#include <map>
#include <memory>
#include <string>
#include <utility>
#include <vector>

// g_SMAPI, g_PLAPI, g_PLID и указатель KHook. Объявить их обязан КАЖДЫЙ файл цели:
// META_CONPRINTF и хуки — макросы поверх этих указателей, а определяет их
// PLUGIN_EXPOSE ровно один раз, внизу plugin.cpp.
PLUGIN_GLOBALVARS();

namespace nm {

class EngineServerInfo;

class NotifyMessagesPlugin final : public ISmmPlugin {
public:
    // Хуки привязываются к методам в конструкторе: индекс vtable считается из
    // указателя на член и движка не трогает. К движку они цепляются в Load (Add)
    // и отцепляются в Unload (Remove).
    NotifyMessagesPlugin();
    ~NotifyMessagesPlugin();  // в plugin.cpp: там EngineServerInfo — полный тип

    bool Load(PluginId id, ISmmAPI* ismm, char* error, size_t maxlen, bool late) override;
    bool Unload(char* error, size_t maxlen) override;

    const char* GetAuthor() override { return "Armatura"; }
    const char* GetName() override { return "NotifyMessages"; }
    const char* GetDescription() override {
        return "Уведомления, реклама, приветствия и мониторинг серверов для CS2";
    }
    const char* GetURL() override { return "https://github.com/Armatura-Create/NotifyMessagesCS2"; }
    const char* GetLicense() override { return "GPL-3.0"; }
    const char* GetVersion() override;
    const char* GetDate() override { return __DATE__; }
    const char* GetLogTag() override { return "NotifyMessages"; }

    // Хуки движка (KHook). Первый аргумент — объект, чей метод перехвачен.
    KHook::Return<void> Hook_OnClientConnected(IServerGameClients*, CPlayerSlot slot, const char* name,
                                               uint64 xuid, const char* networkId, const char* address,
                                               bool fake);
    KHook::Return<void> Hook_ClientPutInServer(IServerGameClients*, CPlayerSlot slot, char const* name, int type,
                                               uint64 xuid);
    KHook::Return<void> Hook_ClientFullyConnect(IServerGameClients*, CPlayerSlot slot);
    KHook::Return<void> Hook_ClientDisconnect(IServerGameClients*, CPlayerSlot slot,
                                              ENetworkDisconnectionReason reason, const char* name, uint64 xuid,
                                              const char* networkId);
    KHook::Return<void> Hook_GameFrame(IServerGameDLL*, bool simulating, bool firstTick, bool lastTick);
    KHook::Return<void> Hook_DispatchConCommand(ICvar*, ConCommandRef command, const CCommandContext& context,
                                                const CCommand& args);
    KHook::Return<bool> Hook_FireEvent(IGameEventManager2* manager, IGameEvent* event, bool dontBroadcast);

    // Команды (commands.cpp). slot < 0 — консоль сервера.
    void CommandServers(int slot);
    void CommandRestartNotify(const CCommand& args);
    void CommandReloadAdvert();
    void CommandCheck();
    void CommandPreview(const CCommand& args);

private:
    // --- plugin.cpp ---
    void RegisterPluginCommands();
    void UnregisterPluginCommands();
    void HookGameEventManager();
    void BuildTimedServices();
    void StopTimedServices();
    std::string ResolveLanguage(uint64_t steamId);
    std::function<void()> RepeatEvery(float intervalSeconds, std::function<void()> action);
    std::function<void()> DelayOnce(float delaySeconds, std::function<void()> action);

    // --- events.cpp ---
    void OnFullyConnected(int slot);
    void OnDisconnected(int slot, const char* name, uint64_t xuid);
    void OnPlayerTeam(IGameEvent* event);
    void FlushTeamChanges();
    void AnnounceTeamChange(const std::string& playerName, int oldTeam, int newTeam);
    void AnnouncePlayerTeamJoin(const std::string& playerName, int team);
    void Broadcast(const std::string& templateText, const Values& values);
    void CachePlayerGeo(uint64_t steamId, const std::string& ip);
    std::string ReadClientLanguage(int slot);
    std::string CurrentName(int slot, const std::string& fallback);
    void RecoverPlayersAfterLateLoad();

    // --- commands.cpp ---
    bool HandleChatCommand(int slot, const char* text);
    void Reply(const std::string& line);
    void Show(const std::string& templateText, MessageType messageType, const std::string& where,
              const std::vector<std::string>& contextTags = {});

    KHook::Virtual<IServerGameDLL, void, bool, bool, bool> _hookGameFrame;
    KHook::Virtual<IServerGameClients, void, CPlayerSlot, const char*, uint64, const char*, const char*, bool>
        _hookOnClientConnected;
    KHook::Virtual<IServerGameClients, void, CPlayerSlot, char const*, int, uint64> _hookClientPutInServer;
    KHook::Virtual<IServerGameClients, void, CPlayerSlot> _hookClientFullyConnect;
    KHook::Virtual<IServerGameClients, void, CPlayerSlot, ENetworkDisconnectionReason, const char*, uint64,
                   const char*>
        _hookClientDisconnect;
    KHook::Virtual<ICvar, void, ConCommandRef, const CCommandContext&, const CCommand&> _hookDispatchConCommand;
    KHook::Virtual<IGameEventManager2, bool, IGameEvent*, bool> _hookFireEvent;

    // Фальшивый «объект» для AddGlobal: KHook читает vtable из первого слова объекта,
    // а у нас есть только сама vtable, найденная по RTTI
    void* _eventManagerVTable = nullptr;

    // Порядок членов — порядок создания: каждый следующий пользуется предыдущими.
    // Логгер — консоль сервера (ConsoleLogger в plugin.cpp)
    std::unique_ptr<ILogger> _logger;
    Config _config;
    std::unique_ptr<GeoIpService> _geoIp;
    SessionService _sessions;
    PlayerRegistry _players;
    LanguageIndex _languageIndex;
    std::unique_ptr<EngineServerInfo> _serverInfo;
    std::unique_ptr<MessageProcessor> _processor;
    EngineOutput _output;
    std::unique_ptr<DisplayService> _display;
    std::unique_ptr<Scheduler> _scheduler;
    std::unique_ptr<ServerStatusService> _serverStatus;
    std::unique_ptr<AdvertisementService> _ads;

    // Антиспам для !servers: доступна любому игроку и дёргает сеть.
    // Чистится при выходе игрока, иначе растёт всё время жизни сервера.
    std::map<uint64_t, double> _serversCooldown;

    // Переходы T<->CT, накопленные за кадр: через границу кадра уходят только
    // строки и числа
    struct TeamChange {
        std::string name;
        int oldTeam;
        int newTeam;
    };
    std::vector<TeamChange> _pendingTeamChanges;
    bool _teamFlushScheduled = false;

    std::string _configDirectory;
    std::string _pluginDirectory;
    bool _loaded = false;
};

extern NotifyMessagesPlugin g_plugin;

}  // namespace nm
