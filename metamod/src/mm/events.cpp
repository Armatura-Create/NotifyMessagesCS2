// Вход, выход, смена команды. Порт Events/NotifyMessages.PlayerEvents.cs и
// Events/NotifyMessages.TeamEvents.cs.
//
// Трассировка [JOIN] n/8 и [LEAVE] n/4 печатается ПЕРЕД опасной операцией, а не
// после: нарушение памяти в нативном слое убивает процесс без стека, и последняя
// успевшая напечататься строка — единственное, что говорит, где это случилось.
#include "core/logger.h"
#include "core/text_formatter.h"
#include "mm/globals.h"
#include "mm/plugin.h"

#include <eiface.h>
#include <igameevents.h>
#include <inetchannelinfo.h>

#include <cstdio>

namespace nm {
namespace {

// Задержка анонса входа — игрок должен успеть догрузиться
constexpr float kJoinAnnounceDelaySeconds = 3.0f;

// Сколько после возврата на новую карту первое попадание в команду — не событие.
// Дольше — игрок сидел в спектаторах, и его выход в команду настоящий.
constexpr double kReturningGraceSeconds = 90.0;

const char* TeamName(int team) {
    switch (team) {
        case 0: return "None";
        case 1: return "Spectators";
        case 2: return "Terrorists";
        case 3: return "Counter-Terrorists";
        default: return "Unknown";
    }
}

const char* ColoredTeam(int team) {
    switch (team) {
        case 2: return "{RED}Terrorists{DEFAULT}";
        case 3: return "{BLUE}Counter-Terrorists{DEFAULT}";
        default: return "{GREY}Spectators{DEFAULT}";
    }
}

// Контекстные значения игрока: одна точка вместо россыпи Replace по коду.
// Нет в кеше — "Unknown"; есть, но пустое (город у мелкого адреса) — пустая строка.
Values PlayerValues(const std::string& playerName, const std::string* country, const std::string* city) {
    return {{"{PLAYERNAME}", playerName},
            {"{COUNTRY}", country != nullptr ? *country : std::string("Unknown")},
            {"{CITY}", city != nullptr ? *city : std::string("Unknown")}};
}

}  // namespace

// Ник на сейчас: игрок мог сменить его после входа
std::string NotifyMessagesPlugin::CurrentName(int slot, const std::string& fallback) {
    const char* name = g_engine != nullptr ? g_engine->GetClientConVarValue(CPlayerSlot(slot), "name") : nullptr;
    return name != nullptr && *name != '\0' ? std::string(name) : fallback;
}

// Язык интерфейса игры — userinfo-квар cl_language ("russian"), сведённый к коду ("ru")
std::string NotifyMessagesPlugin::ReadClientLanguage(int slot) {
    const char* value =
        g_engine != nullptr ? g_engine->GetClientConVarValue(CPlayerSlot(slot), "cl_language") : nullptr;
    const std::string code = steam_language::ToCode(value != nullptr ? value : "");
    _logger->Debug("[Lang] cl_language=\"" + std::string(value != nullptr ? value : "") + "\" -> " +
                   (code.empty() ? std::string("неизвестен") : code));
    return code;
}

void NotifyMessagesPlugin::CachePlayerGeo(uint64_t steamId, const std::string& ip) {
    _logger->Debug("[JOIN] 4/8 IP игрока из OnClientConnected");
    if (ip.empty()) {
        _logger->Debug("[JOIN] 4/8 IP пуст, гео пропущено");
        return;
    }
    _geoIp->UpdatePlayerCache(steamId, ip, _config.defaultLang);
}

void NotifyMessagesPlugin::OnFullyConnected(int slot) {
    _logger->Debug("[JOIN] 1/8 ClientFullyConnect: хук сработал, ищу игрока в слоте");

    const PlayerInfo* player = _players.Find(slot);
    if (player == nullptr || player->fake || player->steamId == 0) {
        _logger->Debug("[JOIN] -- пропуск: слот пуст или бот");
        return;
    }

    _logger->Debug("[JOIN] 2/8 читаю SteamID и ник");
    // Копии, а не ссылки на запись реестра: через таймер уходят только значения
    const uint64_t steamId = player->steamId;
    const std::string playerName = CurrentName(slot, player->name);
    const std::string ip = player->ip;

    _logger->Debug("[JOIN] 3/8 игрок " + playerName + " (SteamID " + std::to_string(steamId) +
                   "), читаю IP и гео");
    CachePlayerGeo(steamId, ip);

    _logger->Debug("[JOIN] 5/8 читаю язык клиента (cl_language)");
    _sessions.SetLanguage(steamId, ReadClientLanguage(slot));

    // При смене карты ClientDisconnect не приходит, а ClientFullyConnect приходит
    // заново. Уже fully-connected игрок — это возврат, а не заход: без анонса, без
    // приветствия, и его первое попадание в команду — тоже не событие.
    const bool returning = _sessions.IsFullyConnected(steamId);
    _logger->Debug(returning ? "[JOIN] 6/8 игрок уже был на сервере — возврат после смены карты"
                             : "[JOIN] 6/8 регистрирую сессию");
    _sessions.AddFullyConnected(steamId);
    _sessions.TryKillAndRemoveConnectionTimer(steamId);

    if (returning) {
        _sessions.MarkReturning(steamId, _scheduler->Now());
        _logger->Debug("[JOIN] 8/8 смена карты: анонс входа и приветствие пропущены");
        return;
    }

    _logger->Debug("[JOIN] 7/8 ставлю таймер анонса входа (3 с)");
    _sessions.SetConnectionTimer(steamId, DelayOnce(kJoinAnnounceDelaySeconds, [this, steamId, playerName]() {
        _logger->Debug("[JOIN-TIMER] сработал для " + playerName);

        if (!_config.joinMessages.empty()) {
            std::string country;
            std::string city;
            const bool hasCountry = _geoIp->TryGetPlayerIso(steamId, &country);
            const bool hasCity = _geoIp->TryGetPlayerCity(steamId, &city);
            _logger->Debug("[JOIN-TIMER] гео из кеша: " + (hasCity ? city : std::string("Unknown")) + ", " +
                           (hasCountry ? country : std::string("Unknown")) + "; рассылаю анонс");

            const Values values = PlayerValues(playerName, hasCountry ? &country : nullptr, hasCity ? &city : nullptr);
            for (const PlayerInfo& recipient : _players.Humans()) {
                const std::string templateText =
                    _processor->GetRandomLocalizedMessage(_config.joinMessages, recipient.steamId);
                if (!templateText.empty()) _display->Print(MessageType::Chat, templateText, recipient.slot, &values);
            }
        }

        _sessions.RemoveConnectionTimer(steamId);
        _logger->Debug("[JOIN-TIMER] анонс входа завершён");
    }));

    if (!_config.welcomeMessage || _config.welcomeMessage->message.empty()) {
        _logger->Debug("[JOIN] 8/8 приветствие не настроено, ConnectFull завершён");
        return;
    }

    const WelcomeMessage welcome = *_config.welcomeMessage;
    char delay[32];
    std::snprintf(delay, sizeof(delay), "%g", welcome.displayDelay);
    _logger->Debug(std::string("[JOIN] 8/8 ставлю таймер приветствия (") + delay + " с, канал " +
                   MessageTypeName(welcome.messageType) + "), ConnectFull завершён");

    // Слот через таймер НЕ проносим: за DisplayDelay игрок может выйти, а его слот —
    // достаться другому. Ищем заново по SteamID.
    const Values welcomeValues = PlayerValues(playerName, nullptr, nullptr);
    _scheduler->Delay(welcome.displayDelay, [this, steamId, welcome, welcomeValues]() {
        _logger->Debug("[WELCOME-TIMER] сработал, ищу игрока " + std::to_string(steamId));

        const PlayerInfo* target = _players.FindBySteamId(steamId);
        if (target == nullptr) {
            _logger->Debug("[WELCOME-TIMER] игрок уже вышел, приветствие пропущено");
            return;
        }

        _logger->Debug(std::string("[WELCOME-TIMER] показываю приветствие в ") + MessageTypeName(welcome.messageType));
        _display->Print(welcome.messageType, welcome.message, target->slot, &welcomeValues);
        _logger->Debug("[WELCOME-TIMER] приветствие показано");
    });
}

void NotifyMessagesPlugin::OnDisconnected(int slot, const char* name, uint64_t xuid) {
    _logger->Debug("[LEAVE] 1/4 ClientDisconnect: хук сработал");

    const PlayerInfo* player = _players.Find(slot);
    const bool fake = player != nullptr ? player->fake : xuid == 0;
    if (fake) {
        _players.Disconnect(slot);
        return;
    }

    _logger->Debug("[LEAVE] 2/4 читаю SteamID и ник");
    const uint64_t steamId = xuid != 0 ? xuid : player->steamId;
    const std::string playerName =
        name != nullptr && *name != '\0' ? std::string(name) : (player != nullptr ? player->name : std::string());

    _logger->Debug("[LEAVE] 3/4 игрок " + playerName + " (SteamID " + std::to_string(steamId) + ") отключился");

    if (_sessions.TryKillAndRemoveConnectionTimer(steamId)) {
        _logger->Debug("  -> Killed connection timer for " + playerName);
    } else if (_sessions.IsFullyConnected(steamId) && !_config.leaveMessages.empty()) {
        std::string country;
        std::string city;
        const bool hasCountry = _geoIp->TryGetPlayerIso(steamId, &country);
        const bool hasCity = _geoIp->TryGetPlayerCity(steamId, &city);
        const Values values = PlayerValues(playerName, hasCountry ? &country : nullptr, hasCity ? &city : nullptr);

        for (const PlayerInfo& recipient : _players.Humans()) {
            if (recipient.steamId == steamId) continue;
            const std::string templateText =
                _processor->GetRandomLocalizedMessage(_config.leaveMessages, recipient.steamId);
            if (!templateText.empty()) _display->Print(MessageType::Chat, templateText, recipient.slot, &values);
        }
    }

    _logger->Debug("[LEAVE] 4/4 чищу состояние игрока " + playerName);

    // Всё, что ключуется по SteamID, чистится здесь — иначе растёт всё время жизни сервера
    _sessions.RemoveFullyConnected(steamId);
    _sessions.RemoveLanguage(steamId);
    _geoIp->RemovePlayer(steamId);
    _serversCooldown.erase(steamId);
    _players.Disconnect(slot);

    _logger->Debug("[LEAVE] Disconnect завершён");
}

// Событие живёт только внутри FireEvent: все поля снимаются здесь, дальше уходят
// строки и числа
void NotifyMessagesPlugin::OnPlayerTeam(IGameEvent* event) {
    const int slot = event->GetPlayerSlot("userid").Get();
    const PlayerInfo* player = _players.Find(slot);
    if (player == nullptr || player->fake || event->GetBool("isbot")) return;

    const int newTeam = event->GetInt("team");
    const int oldTeam = event->GetInt("oldteam");
    const bool disconnect = event->GetBool("disconnect");
    const bool silent = event->GetBool("silent");
    const uint64_t steamId = player->steamId;
    const std::string playerName = CurrentName(slot, player->name);

    if (_config.debug) {
        _logger->Info("[EVENT] " + playerName + " team change: " + TeamName(oldTeam) + " -> " + TeamName(newTeam) +
                      " (silent=" + (silent ? "True" : "False") + ", disconnect=" + (disconnect ? "True" : "False") +
                      ")");
    }

    // Уход с сервера — не смена команды
    if (disconnect || newTeam == 0 || newTeam == oldTeam) return;

    // Движок сам молчит про эту смену (halftime-свап, тихие переводы плагинами) — и мы молчим
    if (silent) {
        _logger->Debug("[TEAM] " + playerName + ": silent-смена, анонс пропущен");
        return;
    }

    // Первое попадание в команду после смены карты — возврат, а не событие
    if (oldTeam == 0 && _sessions.TakeReturning(steamId, _scheduler->Now(), kReturningGraceSeconds)) {
        _logger->Debug("[TEAM] " + playerName + ": команда после смены карты, анонс пропущен");
        return;
    }

    if (oldTeam == 0) {
        AnnouncePlayerTeamJoin(playerName, newTeam);
        return;
    }

    // Переход между командами — через кадр и пачкой, чтобы отсеять смену сторон
    _pendingTeamChanges.push_back({playerName, oldTeam, newTeam});
    if (!_teamFlushScheduled) {
        _teamFlushScheduled = true;
        _scheduler->NextFrame([this]() { FlushTeamChanges(); });
    }
}

void NotifyMessagesPlugin::FlushTeamChanges() {
    _teamFlushScheduled = false;

    std::vector<TeamChange> batch;
    batch.swap(_pendingTeamChanges);
    if (batch.empty()) return;

    std::vector<std::pair<int, int>> changes;
    for (const TeamChange& change : batch) changes.emplace_back(change.oldTeam, change.newTeam);

    if (team_swap::IsMassSwap(changes)) {
        _logger->Debug("[TEAM] смена сторон (" + std::to_string(batch.size()) + " игроков за кадр), анонс пропущен");
        return;
    }

    for (const TeamChange& change : batch) AnnounceTeamChange(change.name, change.oldTeam, change.newTeam);
}

void NotifyMessagesPlugin::AnnounceTeamChange(const std::string& playerName, int oldTeam, int newTeam) {
    if (_config.changeTeamMessage.empty()) return;

    // Значения уходят в ProcessMessage и подставляются ДО рендера — иначе игрок
    // видел бы в чате литеральное «{RED}Terrorists{DEFAULT}»
    Broadcast(_config.changeTeamMessage,
              {{"{PLAYERNAME}", playerName}, {"{TEAM}", ColoredTeam(newTeam)}, {"{OLD_TEAM}", ColoredTeam(oldTeam)}});
}

void NotifyMessagesPlugin::AnnouncePlayerTeamJoin(const std::string& playerName, int team) {
    if (_config.joinTeamMessage.empty()) return;
    Broadcast(_config.joinTeamMessage, {{"{PLAYERNAME}", playerName}, {"{TEAM}", ColoredTeam(team)}});
}

// Каждому игроку — на его языке: локализация идёт по SteamID получателя
void NotifyMessagesPlugin::Broadcast(const std::string& templateText, const Values& values) {
    for (const PlayerInfo& recipient : _players.Humans()) {
        _display->Print(MessageType::Chat, templateText, recipient.slot, &values);
    }
}

// Плагин загрузили на живой сервер: игроков, пришедших раньше, хуки не видели.
// Всё берётся из движка по номеру слота — это функции движка с проверкой слота,
// а не чтение сущностей, так что риска GetPlayerFromSlot здесь нет.
void NotifyMessagesPlugin::RecoverPlayersAfterLateLoad() {
    const CGlobalVars* globals = g_engine != nullptr ? g_engine->GetServerGlobals() : nullptr;
    if (globals == nullptr) return;

    int recovered = 0;
    for (int slot = 0; slot < globals->maxClients; ++slot) {
        INetChannelInfo* channel = g_engine->GetPlayerNetInfo(CPlayerSlot(slot));
        if (channel == nullptr) continue;  // пустой слот или бот
        const uint64_t steamId = g_engine->GetClientXUID(CPlayerSlot(slot));
        if (steamId == 0) continue;

        const std::string name = CurrentName(slot, std::string());
        const char* address = channel->GetAddress();
        const std::string ip = GeoIpService::ExtractIp(address != nullptr ? address : "");

        _players.Connect(slot, steamId, name, ip, false);
        _players.PutInServer(slot, steamId, name);
        CachePlayerGeo(steamId, ip);
        // Иначе первая смена карты анонсировала бы всех как новых
        _sessions.AddFullyConnected(steamId);
        ++recovered;
    }

    _logger->Info("[Load] Поздняя загрузка: подхвачено игроков — " + std::to_string(recovered));
}

}  // namespace nm
