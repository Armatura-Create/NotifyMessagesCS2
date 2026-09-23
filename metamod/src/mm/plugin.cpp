#include "mm/plugin.h"

#include "core/logger.h"
#include "mm/globals.h"
#include "mm/rtti.h"
#include "mm/version.h"

#include <eiface.h>
#include <engine/igameeventsystem.h>
#include <icvar.h>
#include <igameevents.h>
#include <networksystem/inetworkmessages.h>

#include <chrono>
#include <cstring>
#include <ctime>
#include <utility>

namespace nm {

IVEngineServer2* g_engine = nullptr;
ISource2Server* g_server = nullptr;
IServerGameClients* g_gameClients = nullptr;
IGameEventSystem* g_gameEventSystem = nullptr;
INetworkMessages* g_networkMessages = nullptr;
IGameEventManager2* g_gameEventManager = nullptr;

NotifyMessagesPlugin g_plugin;

namespace {

double MonotonicSeconds() {
    return std::chrono::duration<double>(std::chrono::steady_clock::now().time_since_epoch()).count();
}

std::string ReadConVar(const char* name, const char* fallback) {
    if (g_pCVar == nullptr) return fallback;
    ConVarRefAbstract convar(name);
    if (!convar.IsValidRef()) return fallback;
    const CUtlString value = convar.GetString();
    return value.Get() != nullptr ? std::string(value.Get()) : std::string(fallback);
}

}  // namespace

// Формат — как PluginLogger C#-целей: [время] [NotifyMessages] [УРОВЕНЬ] текст.
// Debug читает флаг через указатель на конфиг, поэтому подхватывается сразу после
// mm_reload_advert. Главный поток: фон шлёт логи через Scheduler::NextFrame.
class ConsoleLogger final : public ILogger {
public:
    explicit ConsoleLogger(const Config* config) : _config(config) {}

    void Info(const std::string& message) override { Write("INFO", message); }
    void Error(const std::string& message) override { Write("ERROR", message); }
    void Debug(const std::string& message) override {
        if (_config->debug) Write("DEBUG", message);
    }

private:
    static void Write(const char* level, const std::string& message) {
        const std::time_t now = std::time(nullptr);
        std::tm local{};
#ifdef _WIN32
        localtime_s(&local, &now);
#else
        localtime_r(&now, &local);
#endif
        char stamp[32];
        std::strftime(stamp, sizeof(stamp), "%Y-%m-%d %H:%M:%S", &local);
        META_CONPRINTF("[%s] [NotifyMessages] [%s] %s\n", stamp, level, message.c_str());
    }

    const Config* _config;
};

// Факты о сервере для {MAP} {SERVERNAME} … Каждое свойство — движок, поэтому
// читается лениво, из событий, команд и таймеров, а не в Load().
class EngineServerInfo final : public IServerInfoSource {
public:
    explicit EngineServerInfo(const PlayerRegistry* players) : _players(players) {}

    std::string MapName() override {
        const CGlobalVars* globals = g_engine != nullptr ? g_engine->GetServerGlobals() : nullptr;
        return globals != nullptr ? std::string(globals->mapname.ToCStr()) : std::string();
    }
    std::string Hostname() override { return ReadConVar("hostname", "Server"); }
    std::string Ip() override { return ReadConVar("ip", "127.0.0.1"); }
    std::string Port() override { return ReadConVar("hostport", "27015"); }
    int MaxPlayers() override {
        const CGlobalVars* globals = g_engine != nullptr ? g_engine->GetServerGlobals() : nullptr;
        return globals != nullptr ? globals->maxClients : 0;
    }
    // Все в игре, включая ботов, — как «пешка валидна» у C#-целей
    int Players() override { return _players->CountInGame(); }

private:
    const PlayerRegistry* _players;
};

const char* NotifyMessagesPlugin::GetVersion() { return NM_VERSION; }

// pre/post — как в C#-целях: подключение запоминаем до движка, чат слушаем до
// движка (чтобы скрыть /servers), FireEvent — до движка (чтобы выставить
// bDontBroadcast), остальное — после.
NotifyMessagesPlugin::NotifyMessagesPlugin()
    : _hookGameFrame(&IServerGameDLL::GameFrame, this, nullptr, &NotifyMessagesPlugin::Hook_GameFrame),
      _hookOnClientConnected(&IServerGameClients::OnClientConnected, this,
                             &NotifyMessagesPlugin::Hook_OnClientConnected, nullptr),
      _hookClientPutInServer(&IServerGameClients::ClientPutInServer, this, nullptr,
                             &NotifyMessagesPlugin::Hook_ClientPutInServer),
      _hookClientFullyConnect(&IServerGameClients::ClientFullyConnect, this, nullptr,
                              &NotifyMessagesPlugin::Hook_ClientFullyConnect),
      _hookClientDisconnect(&IServerGameClients::ClientDisconnect, this, nullptr,
                            &NotifyMessagesPlugin::Hook_ClientDisconnect),
      _hookDispatchConCommand(&ICvar::DispatchConCommand, this, &NotifyMessagesPlugin::Hook_DispatchConCommand,
                              nullptr),
      _hookFireEvent(&IGameEventManager2::FireEvent, this, &NotifyMessagesPlugin::Hook_FireEvent, nullptr) {}

NotifyMessagesPlugin::~NotifyMessagesPlugin() = default;

bool NotifyMessagesPlugin::Load(PluginId id, ISmmAPI* ismm, char* error, size_t maxlen, bool late) {
    PLUGIN_SAVEVARS();

    GET_V_IFACE_CURRENT(GetEngineFactory, g_engine, IVEngineServer2, SOURCE2ENGINETOSERVER_INTERFACE_VERSION);
    GET_V_IFACE_CURRENT(GetEngineFactory, g_pCVar, ICvar, CVAR_INTERFACE_VERSION);
    GET_V_IFACE_ANY(GetServerFactory, g_server, ISource2Server, SOURCE2SERVER_INTERFACE_VERSION);
    GET_V_IFACE_ANY(GetServerFactory, g_gameClients, IServerGameClients, SOURCE2GAMECLIENTS_INTERFACE_VERSION);
    GET_V_IFACE_ANY(GetEngineFactory, g_gameEventSystem, IGameEventSystem, GAMEEVENTSYSTEM_INTERFACE_VERSION);
    GET_V_IFACE_ANY(GetEngineFactory, g_networkMessages, INetworkMessages, NETWORKMESSAGES_INTERFACE_VERSION);

    const std::string base = g_SMAPI->GetBaseDir();
    _pluginDirectory = base + "/addons/NotifyMessages";
    // Конфиги — в общем дереве addons/configs, рядом с остальными нативными
    // плагинами: владельцу сервера не нужно помнить отдельное правило для этого
    _configDirectory = base + "/addons/configs/NotifyMessages";

    // Сервисы создаются в фиксированном порядке: каждый следующий получает
    // предыдущие. Движок здесь не трогается — globals ещё может не быть.
    _logger.reset(new ConsoleLogger(&_config));
    _config = ConfigService(_logger.get()).LoadOrCreate(_configDirectory);
    _logger->Info(std::string("NotifyMessages v") + NM_VERSION + " (Metamod:Source)");

    // Базы GeoLite2 лежат рядом с бинарником плагина
    _geoIp.reset(new GeoIpService(_pluginDirectory, _logger.get()));
    _languageIndex = LanguageIndex::Build(_config);
    _serverInfo.reset(new EngineServerInfo(&_players));
    _processor.reset(new MessageProcessor(&_config, [this](uint64_t steamId) { return ResolveLanguage(steamId); },
                                          _serverInfo.get()));
    _display.reset(new DisplayService(&_config, _processor.get(), &_players, &_output, _logger.get()));
    _scheduler.reset(new Scheduler(MonotonicSeconds));

    _hookGameFrame.Add(g_server);
    _hookOnClientConnected.Add(g_gameClients);
    _hookClientPutInServer.Add(g_gameClients);
    _hookClientFullyConnect.Add(g_gameClients);
    _hookClientDisconnect.Add(g_gameClients);
    _hookDispatchConCommand.Add(g_pCVar);
    HookGameEventManager();

    RegisterPluginCommands();
    BuildTimedServices();

    // Плагин загрузили на живой сервер: игроков, пришедших раньше, хуки не видели.
    // Движок трогаем не здесь, а в первом кадре — там он гарантированно готов.
    if (late) _scheduler->NextFrame([this]() { RecoverPlayersAfterLateLoad(); });

    _loaded = true;
    (void)id;
    (void)error;
    (void)maxlen;
    return true;
}

bool NotifyMessagesPlugin::Unload(char* error, size_t maxlen) {
    (void)error;
    (void)maxlen;
    _loaded = false;

    // Команды и хуки живут в выгружаемой библиотеке: оставленный движку указатель
    // на неё — падение при следующем вызове
    UnregisterPluginCommands();
    _hookGameFrame.Remove(g_server);
    _hookOnClientConnected.Remove(g_gameClients);
    _hookClientPutInServer.Remove(g_gameClients);
    _hookClientFullyConnect.Remove(g_gameClients);
    _hookClientDisconnect.Remove(g_gameClients);
    _hookDispatchConCommand.Remove(g_pCVar);
    if (_eventManagerVTable != nullptr) {
        _hookFireEvent.RemoveGlobal(reinterpret_cast<IGameEventManager2*>(&_eventManagerVTable));
    }

    // Фоновый опрос дожидаемся ДО всего остального: его поток исполняет код
    // этой библиотеки и пишет в её память
    StopTimedServices();

    // Таймеры сессий останавливаются через планировщик — он ещё жив
    _sessions.Clear();
    _scheduler.reset();

    _display.reset();
    _processor.reset();
    _serverInfo.reset();
    _geoIp.reset();
    _players.Clear();
    _serversCooldown.clear();
    _pendingTeamChanges.clear();
    _teamFlushScheduled = false;
    g_gameEventManager = nullptr;
    return true;
}

// Менеджер игровых событий фабрика не отдаёт. vtable CGameEventManager ищется по
// RTTI, FireEvent перехватывается глобально — у всех объектов этого класса,
// а сам объект приходит первым аргументом хука.
void NotifyMessagesPlugin::HookGameEventManager() {
    const char* error = "";
    void* vtable = rtti::FindServerVTable("CGameEventManager", &error);
    if (vtable == nullptr) {
        _logger->Error(std::string("[Events] CGameEventManager не найден (") + error +
                       "). CenterHtml будет выводиться обычным центром, смена команды не "
                       "анонсируется, штатные сообщения движка о выходе и смене команды не скрываются");
        return;
    }

    _eventManagerVTable = vtable;
    _hookFireEvent.AddGlobal(reinterpret_cast<IGameEventManager2*>(&_eventManagerVTable));
    _logger->Debug("[Events] CGameEventManager найден по RTTI, FireEvent перехвачен");
}

std::function<void()> NotifyMessagesPlugin::RepeatEvery(float intervalSeconds, std::function<void()> action) {
    const Scheduler::Id id = _scheduler->Repeat(intervalSeconds, std::move(action));
    return [this, id]() {
        if (_scheduler) _scheduler->Cancel(id);
    };
}

std::function<void()> NotifyMessagesPlugin::DelayOnce(float delaySeconds, std::function<void()> action) {
    const Scheduler::Id id = _scheduler->Delay(delaySeconds, std::move(action));
    return [this, id]() {
        if (_scheduler) _scheduler->Cancel(id);
    };
}

// Сервисы, которые кешируют Config и держат таймеры. Пересоздаются в
// mm_reload_advert — любой новый такой сервис обязан попасть сюда.
void NotifyMessagesPlugin::BuildTimedServices() {
    const auto repeat = [this](float interval, std::function<void()> action) {
        return RepeatEvery(interval, std::move(action));
    };

    // Возврат в главный поток из фона: Scheduler::NextFrame потокобезопасен
    _serverStatus.reset(new ServerStatusService(
        _config.servers, _logger.get(), repeat,
        [this](std::function<void()> task) { _scheduler->NextFrame(std::move(task)); }, MonotonicSeconds));
    _ads.reset(new AdvertisementService(_config.ads, _logger.get(), repeat,
                                        [this](MessageType channel, const std::string& message) {
                                            _display->Print(channel, message);
                                        }));

    // Необязательные подсистемы: их отказ не мешает остальному, и исключений
    // здесь нет — ошибки конфига уже превратились в значения по умолчанию
    _serverStatus->InitialQuery();
    _ads->Start();
    _serverStatus->Start();
}

void NotifyMessagesPlugin::StopTimedServices() {
    _ads.reset();
    _serverStatus.reset();  // деструктор ждёт фоновый проход
}

// Язык игрока: язык интерфейса игры (cl_language), потом страна, потом дефолт.
// Если на входе квар был пуст, дочитываем его при первом сообщении и кешируем.
std::string NotifyMessagesPlugin::ResolveLanguage(uint64_t steamId) {
    std::string language = _sessions.GetLanguage(steamId);

    if (language.empty() && steamId != 0) {
        const PlayerInfo* player = _players.FindBySteamId(steamId);
        if (player != nullptr) {
            language = ReadClientLanguage(player->slot);
            _sessions.SetLanguage(steamId, language);
        }
    }

    return _languageIndex.Resolve(language, _geoIp->GetIsoForSteamId(steamId), _config.defaultLang);
}

KHook::Return<void> NotifyMessagesPlugin::Hook_GameFrame(IServerGameDLL*, bool simulating, bool firstTick,
                                                         bool lastTick) {
    (void)simulating;
    (void)firstTick;
    (void)lastTick;

    // Кадр — единственное место, где мы гарантированно в главном потоке
    if (!_loaded) return {KHook::Action::Ignore};

    _scheduler->RunFrame();
    _display->OnTick(MonotonicSeconds());
    return {KHook::Action::Ignore};
}

KHook::Return<void> NotifyMessagesPlugin::Hook_OnClientConnected(IServerGameClients*, CPlayerSlot slot,
                                                                 const char* name, uint64 xuid,
                                                                 const char* networkId, const char* address,
                                                                 bool fake) {
    (void)networkId;
    // Единственное место, где движок сам отдаёт IP игрока
    _players.Connect(slot.Get(), xuid, name != nullptr ? name : "",
                     GeoIpService::ExtractIp(address != nullptr ? address : ""), fake);
    return {KHook::Action::Ignore};
}

KHook::Return<void> NotifyMessagesPlugin::Hook_ClientPutInServer(IServerGameClients*, CPlayerSlot slot,
                                                                 char const* name, int type, uint64 xuid) {
    (void)type;
    _players.PutInServer(slot.Get(), xuid, name != nullptr ? name : "");
    return {KHook::Action::Ignore};
}

KHook::Return<void> NotifyMessagesPlugin::Hook_ClientFullyConnect(IServerGameClients*, CPlayerSlot slot) {
    if (_loaded) OnFullyConnected(slot.Get());
    return {KHook::Action::Ignore};
}

KHook::Return<void> NotifyMessagesPlugin::Hook_ClientDisconnect(IServerGameClients*, CPlayerSlot slot,
                                                                ENetworkDisconnectionReason reason,
                                                                const char* name, uint64 xuid,
                                                                const char* networkId) {
    (void)reason;
    (void)networkId;
    if (_loaded) {
        OnDisconnected(slot.Get(), name, xuid);
    } else {
        _players.Disconnect(slot.Get());
    }
    return {KHook::Action::Ignore};
}

KHook::Return<void> NotifyMessagesPlugin::Hook_DispatchConCommand(ICvar*, ConCommandRef command,
                                                                  const CCommandContext& context,
                                                                  const CCommand& args) {
    (void)command;

    // -1 — консоль сервера: там команды и так работают напрямую
    const int slot = context.GetPlayerSlot().Get();
    if (!_loaded || slot < 0 || args.ArgC() < 2) return {KHook::Action::Ignore};

    const char* verb = args.Arg(0);
    if (verb == nullptr || (std::strcmp(verb, "say") != 0 && std::strcmp(verb, "say_team") != 0)) {
        return {KHook::Action::Ignore};
    }

    // Проглатываем ТОЛЬКО свою команду и только с «/»: плагин, съедающий чужой
    // say, ломает чат-плагины рядом, и найти причину будет нечем
    if (HandleChatCommand(slot, args.Arg(1))) return {KHook::Action::Supersede};
    return {KHook::Action::Ignore};
}

// Все игровые события проходят здесь. Штатные сообщения о выходе и смене команды
// движок рисует сам — их глушим (bDontBroadcast), как EventPlayer*Pre в C#-целях:
// вместо них игрок видит сообщения плагина на своём языке.
KHook::Return<bool> NotifyMessagesPlugin::Hook_FireEvent(IGameEventManager2* manager, IGameEvent* event,
                                                         bool dontBroadcast) {
    g_gameEventManager = manager;
    if (!_loaded || event == nullptr) return {KHook::Action::Ignore};

    const char* name = event->GetName();
    if (name == nullptr) return {KHook::Action::Ignore};

    const bool team = std::strcmp(name, "player_team") == 0;
    if (!team && std::strcmp(name, "player_disconnect") != 0) return {KHook::Action::Ignore};

    if (team) OnPlayerTeam(event);

    if (dontBroadcast) return {KHook::Action::Ignore};
    return KHook::Recall(&IGameEventManager2::FireEvent, KHook::Return<bool>{KHook::Action::Ignore, false}, manager,
                         event, true);
}

}  // namespace nm

PLUGIN_EXPOSE(NotifyMessagesPlugin, nm::g_plugin);
