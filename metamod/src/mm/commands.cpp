// Команды плагина. Порт Commands/NotifyMessages.Commands.cs и PreviewCommands.cs.
//
// | Действие                     | CSSharp             | Эта цель                      |
// |------------------------------|---------------------|-------------------------------|
// | список серверов, игрок       | css_servers         | mm_servers, !servers, /servers|
// | оповещение о рестарте        | css_restart_notify  | mm_restart_notify (консоль)   |
// | перечитать конфиги           | css_reload_advert   | mm_reload_advert (консоль)    |
// | проверить шаблоны            | css_nm_check        | mm_nm_check (консоль)         |
// | показать шаблон              | css_nm_preview      | mm_nm_preview (консоль)       |
//
// Админские команды — только с консоли сервера или через rcon: своей системы прав
// у Metamod нет, а изобретать её ради четырёх команд значило бы завести ещё один
// файл с правами, который разъедется с настоящим.
#include "core/logger.h"
#include "core/template_diagnostics.h"
#include "core/text_formatter.h"
#include "mm/globals.h"
#include "mm/plugin.h"

#include <convar.h>

#include <cctype>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>

namespace nm {
namespace {

// Антиспам: команда доступна любому игроку и дёргает сетевые запросы
constexpr double kServersCooldownSeconds = 10.0;

constexpr const char* kPreviewUsage = "<welcome | ad <номер> | servers | key <ключ> | raw <текст>>";

bool FromConsole(const CCommandContext& context) { return context.GetPlayerSlot().Get() == -1; }

CON_COMMAND_F(mm_servers, "Показать список серверов из кеша",
              FCVAR_GAMEDLL | FCVAR_RELEASE | FCVAR_CLIENT_CAN_EXECUTE) {
    (void)args;
    if (FromConsole(context)) {
        META_CONPRINTF("[NotifyMessages] mm_servers — команда игрока (в чате: !servers)\n");
        return;
    }
    g_plugin.CommandServers(context.GetPlayerSlot().Get());
}

CON_COMMAND_F(mm_restart_notify, "mm_restart_notify <seconds> — оповестить игроков о рестарте",
              FCVAR_GAMEDLL | FCVAR_RELEASE) {
    if (FromConsole(context)) g_plugin.CommandRestartNotify(args);
}

CON_COMMAND_F(mm_reload_advert, "Перечитать все конфиги NotifyMessages", FCVAR_GAMEDLL | FCVAR_RELEASE) {
    (void)args;
    if (FromConsole(context)) g_plugin.CommandReloadAdvert();
}

CON_COMMAND_F(mm_nm_check, "Проверить все шаблоны конфигурации NotifyMessages", FCVAR_GAMEDLL | FCVAR_RELEASE) {
    (void)args;
    if (FromConsole(context)) g_plugin.CommandCheck();
}

CON_COMMAND_F(mm_nm_preview, "mm_nm_preview <welcome | ad <n> | servers | key <k> | raw <текст>>",
              FCVAR_GAMEDLL | FCVAR_RELEASE) {
    if (FromConsole(context)) g_plugin.CommandPreview(args);
}

std::string Lower(const char* value) {
    std::string out = value != nullptr ? value : "";
    for (char& c : out) c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
    return out;
}

// int.TryParse: пробелы по краям и знак допустимы, остальное — нет
bool ParseInt(const char* text, long* out) {
    if (text == nullptr) return false;
    char* end = nullptr;
    const long value = std::strtol(text, &end, 10);
    if (end == text) return false;
    while (*end != '\0' && std::isspace(static_cast<unsigned char>(*end))) ++end;
    if (*end != '\0') return false;
    *out = value;
    return true;
}

// TimeSpan.ToString(@"mm\:ss"): минуты — без часов, как в C#
std::string FormatMinutesSeconds(long seconds) {
    char buffer[16];
    std::snprintf(buffer, sizeof(buffer), "%02ld:%02ld", (seconds / 60) % 60, seconds % 60);
    return buffer;
}

}  // namespace

void NotifyMessagesPlugin::RegisterPluginCommands() {
    // На Source 2 ConCommand, созданный CON_COMMAND_F, движку ещё не известен: его
    // отдаёт ConVar_Register. Metamod должен знать, чьи это команды, — отсюда макрос
    META_CONVAR_REGISTER(FCVAR_RELEASE | FCVAR_GAMEDLL);
}

void NotifyMessagesPlugin::UnregisterPluginCommands() { ConVar_Unregister(); }

void NotifyMessagesPlugin::Reply(const std::string& line) { _logger->Info(line); }

// "!servers" видно в чате и выполняется; "/servers" выполняется молча — как
// чат-триггеры CounterStrikeSharp. true — сообщение не пускать в чат.
bool NotifyMessagesPlugin::HandleChatCommand(int slot, const char* text) {
    if (text == nullptr || (*text != '!' && *text != '/')) return false;
    const bool silent = *text == '/';

    std::string word;
    for (const char* at = text + 1; *at != '\0' && *at != ' '; ++at) {
        word.push_back(static_cast<char>(std::tolower(static_cast<unsigned char>(*at))));
    }
    if (word != "servers") return false;

    const PlayerInfo* player = _players.Find(slot);
    if (player == nullptr || player->fake) return false;

    // Ответ — следующим кадром, после самого сообщения игрока в чате.
    // Через кадр уходит SteamID, слот ищется заново.
    const uint64_t steamId = player->steamId;
    _scheduler->NextFrame([this, steamId]() {
        const PlayerInfo* target = _players.FindBySteamId(steamId);
        if (target != nullptr) CommandServers(target->slot);
    });
    return silent;
}

void NotifyMessagesPlugin::CommandServers(int slot) {
    const PlayerInfo* player = _players.Find(slot);
    if (player == nullptr || player->fake || !player->inGame) return;

    if (!_config.servers) {
        _logger->Debug("[COMMAND] Config.Servers is null");
        return;
    }
    if (!_config.servers->enabled) {
        _output.Chat(slot, "[Servers] Server monitoring is disabled in Servers.json");
        return;
    }
    if (_config.servers->list.empty()) {
        _output.Chat(slot, "[Servers] No servers configured in Servers.json");
        return;
    }

    const uint64_t steamId = player->steamId;
    const std::string playerName = player->name;
    const double now = _scheduler->Now();

    const auto last = _serversCooldown.find(steamId);
    if (last != _serversCooldown.end()) {
        const double elapsed = now - last->second;
        if (elapsed < kServersCooldownSeconds) {
            char text[128];
            std::snprintf(text, sizeof(text), "[Servers] Please wait %.0f second(s) before using this command again.",
                          std::ceil(kServersCooldownSeconds - elapsed));
            _output.Chat(slot, text);
            return;
        }
    }
    _serversCooldown[steamId] = now;

    _logger->Debug("[COMMAND] mm_servers by " + playerName + ", showing " +
                   std::to_string(_config.servers->list.size()) + " server(s)");

    // Текущие данные из кеша — сразу; обновление кеша — в фоне к следующему запросу
    _serverStatus->AnnounceToPlayer(playerName, _config.titleAnnounceServers,
                                    [this, slot](MessageType channel, const std::string& message) {
                                        _display->Print(channel, message, slot);
                                    });
    _serverStatus->TriggerBackgroundUpdate();
}

// Точка интеграции с внешним апдейтером: тот шлёт в консоль сервера
// mm_restart_notify <секунды> вместо голого say — и сообщение уходит игрокам с
// цветами и на их языке (Settings.RestartNotify + Messages.json).
void NotifyMessagesPlugin::CommandRestartNotify(const CCommand& args) {
    long seconds = 0;
    if (!ParseInt(args.Arg(1), &seconds) || seconds < 0 || seconds > 86400) {
        _logger->Info("[ERROR] Use: mm_restart_notify <seconds> (0-86400)");
        return;
    }

    if (!_config.restartNotify || !_config.restartNotify->enabled) {
        _logger->Debug("[COMMAND] mm_restart_notify skipped: RestartNotify disabled in Settings.json");
        return;
    }

    // Точная отсечка из конфига, иначе общий шаблон с {SECONDS}
    const std::string* templateText = _config.restartNotify->ResolveTemplate(static_cast<int>(seconds));
    if (templateText == nullptr) {
        _logger->Info("[COMMAND] mm_restart_notify: no message template configured");
        return;
    }

    // {SECONDS}/{TIME_RESTART} долетают и внутрь текстов из Messages.json:
    // ProcessMessage подставляет значения после локализации, но до рендера
    const Values values = {{"{SECONDS}", std::to_string(seconds)}, {"{TIME_RESTART}", FormatMinutesSeconds(seconds)}};
    const MessageType type = _config.restartNotify->messageType;

    _logger->Info("[COMMAND] mm_restart_notify " + std::to_string(seconds) + "s -> " + MessageTypeName(type));
    _display->Print(type, *templateText, -1, &values);
}

void NotifyMessagesPlugin::CommandReloadAdvert() {
    _logger->Info("[COMMAND] mm_reload_advert executed by Console");

    // Таймеры держат старые сервисы — остановить до замены конфига
    StopTimedServices();

    _config = ConfigService(_logger.get()).LoadOrCreate(_configDirectory);
    // Индекс языков кеширует Config — пересобираем, иначе он останется на старом
    _languageIndex = LanguageIndex::Build(_config);
    _serversCooldown.clear();

    for (const PlayerInfo& player : _players.Humans()) CachePlayerGeo(player.steamId, player.ip);

    BuildTimedServices();
    _logger->Info("[NotifyMessages] configuration successfully reloaded!");
}

void NotifyMessagesPlugin::CommandCheck() {
    const std::vector<TemplateIssue> issues = CollectIssues(_config);
    if (issues.empty()) {
        Reply("[Check] Проблем в шаблонах не найдено.");
        return;
    }

    int errors = 0;
    Reply("═══ NotifyMessages: проверка шаблонов ═══");
    for (const TemplateIssue& issue : issues) {
        if (issue.severity == TemplateSeverity::Error) ++errors;
        Reply("  " + issue.ToString());
    }
    Reply("[Check] Всего: " + std::to_string(issues.size()) + ", из них ошибок: " + std::to_string(errors));
}

// Рендерит шаблон ТЕМ ЖЕ путём, что и боевые сообщения, и печатает диагностику.
// С консоли сервера показать на экране нечего — печатается текст без управляющих кодов.
void NotifyMessagesPlugin::Show(const std::string& templateText, MessageType messageType, const std::string& where,
                                const std::vector<std::string>& contextTags) {
    if (templateText.empty()) return;

    for (const TemplateIssue& issue : AnalyzeTemplate(templateText, _config, where, contextTags)) {
        Reply("  " + issue.ToString());
    }

    const std::string processed = _processor->ProcessMessage(templateText, 0, messageType);
    _logger->Info("[Preview] " + where + " → " + MessageTypeName(messageType) + ": " +
                  text::StripColorCodes(processed));
}

void NotifyMessagesPlugin::CommandPreview(const CCommand& args) {
    const std::string target = Lower(args.Arg(1));

    if (target == "welcome") {
        if (!_config.welcomeMessage || _config.welcomeMessage->message.empty()) {
            Reply("[Preview] WelcomeMessage не настроен в Settings.json");
            return;
        }
        const std::string templateText =
            text::ReplaceIgnoreCase(_config.welcomeMessage->message, "{PLAYERNAME}", "TestPlayer");
        Show(templateText, _config.welcomeMessage->messageType, "Settings.json → WelcomeMessage",
             context::kPlayerNameOnly);
        return;
    }

    if (target == "ad") {
        if (_config.ads.empty()) {
            Reply("[Preview] В Ads.json нет ни одного блока");
            return;
        }
        long number = 0;
        if (!ParseInt(args.Arg(2), &number) || number < 1 || number > static_cast<long>(_config.ads.size())) {
            Reply("[Preview] Укажите номер блока от 1 до " + std::to_string(_config.ads.size()));
            return;
        }

        const std::string index = std::to_string(number);
        const Advertisement& ad = _config.ads[static_cast<size_t>(number - 1)];
        if (ad.messages.empty()) {
            Reply("[Preview] Блок #" + index + " пуст");
            return;
        }

        // Идём по messages напрямую, а НЕ через NextMessages: тот сдвигает ротацию
        for (size_t i = 0; i < ad.messages.size(); ++i) {
            for (const auto& channel : ad.messages[i]) {
                const std::string where =
                    "Ads.json → блок #" + index + ", сообщение #" + std::to_string(i + 1) + ", " + channel.first;
                MessageType type = MessageType::Chat;
                if (!ParseMessageType(channel.first, &type)) {
                    Reply("[Preview] " + where + ": неизвестный канал «" + channel.first +
                          "». Допустимые: Chat, Center, CenterHtml, Console, Alert");
                    continue;
                }
                Show(channel.second, type, where);
            }
        }
        return;
    }

    if (target == "servers") {
        if (!_config.servers || !_config.servers->enabled) {
            Reply("[Preview] Мониторинг серверов выключен: Servers.json → \"Enabled\": true");
            return;
        }
        if (!_config.titleAnnounceServers.empty()) {
            Show(_config.titleAnnounceServers, MessageType::Chat, "Settings.json → TitleAnnounceServers");
        }
        const std::vector<ServerCacheEntry> snapshot = _serverStatus->GetSnapshot();
        if (snapshot.empty()) {
            Reply("[Preview] Кеш серверов пуст — опрос ещё не завершился");
            return;
        }
        for (const ServerCacheEntry& entry : snapshot) {
            Show(entry.chat, MessageType::Chat, "Servers.json → MessageTemplate");
        }
        return;
    }

    if (target == "key") {
        const std::string key = args.Arg(2) != nullptr ? args.Arg(2) : "";
        if (key.empty()) {
            Reply("[Preview] Использование: mm_nm_preview key <ключ из Messages.json>");
            return;
        }
        if (_config.languageMessages.count(key) == 0) {
            Reply("[Preview] Ключа «" + key + "» нет в Messages.json → LanguageMessages");
            return;
        }
        Show("{" + key + "}", MessageType::Chat, "Messages.json → " + key);
        return;
    }

    if (target == "raw") {
        // Текст — из строки команды целиком, а не из токенов: токенизатор движка
        // режет фигурные скобки, и "{RED}тест" пришёл бы как "{ RED } тест"
        std::string rest = args.ArgS() != nullptr ? args.ArgS() : "";
        size_t at = 0;
        while (at < rest.size() && std::isspace(static_cast<unsigned char>(rest[at]))) ++at;
        at += 3;  // "raw"
        while (at < rest.size() && std::isspace(static_cast<unsigned char>(rest[at]))) ++at;
        const std::string templateText = at < rest.size() ? rest.substr(at) : std::string();

        if (templateText.empty()) {
            Reply("[Preview] Использование: mm_nm_preview raw <текст с тегами>");
            return;
        }
        Show(templateText, MessageType::Chat, "raw");
        return;
    }

    Reply(std::string("[Preview] Использование: mm_nm_preview ") + kPreviewUsage);
}

}  // namespace nm
