// Модель конфигурации и её загрузка.
//
// ФОРМАТ — КОНТРАКТ между целями: Settings.json, Messages.json, Ads.json и Servers.json
// переносятся между cssharp/, swiftly/ и metamod/ без правок. Поля, их типы, значения
// по умолчанию и семантика повторяют Models/ConfigModels.cs один в один.
//
// Добавление настройки — четыре правки: поле здесь → строка чтения в config.cpp →
// значение в тексте по умолчанию (config_defaults.cpp) → свойство в схеме там же.
// Отдельного шага «склейки частей», как MergeParts в C#, здесь нет: каждый файл
// читается в копию Config и принимается целиком или не принимается вовсе.
#pragma once

#include <cstdint>
#include <map>
#include <optional>
#include <string>
#include <utility>
#include <vector>

namespace nm {

class ILogger;

// Сравнение без учёта регистра. Только ASCII: ключи и коды языков в конфиге —
// латиница ("prefix", "RU"), а C# сравнивает их через OrdinalIgnoreCase.
struct CaseInsensitiveLess {
    bool operator()(const std::string& a, const std::string& b) const;
};

template <typename V>
using CiMap = std::map<std::string, V, CaseInsensitiveLess>;

// Пары в порядке файла. Там, где C# держит Dictionary, а порядок заметен
// (замена имён карт, каналы одного рекламного сообщения), важен порядок из JSON.
using OrderedPairs = std::vector<std::pair<std::string, std::string>>;

// Канал вывода — один тип на весь плагин, как MessageType в C#.
enum class MessageType { Chat = 0, Center = 1, CenterHtml = 2, Console = 3, Alert = 4 };

// Имя канала без учёта регистра ("chat", "CenterHtml") или число ("2"), как
// Enum.TryParse(ignoreCase: true). false — такого канала нет.
bool ParseMessageType(const std::string& text, MessageType* out);
const char* MessageTypeName(MessageType type);

struct WelcomeMessage {
    MessageType messageType = MessageType::Chat;
    std::string message;
    float displayDelay = 2.0f;
};

// Оповещение о рестарте: точка интеграции с внешним апдейтером (mm_restart_notify).
struct RestartNotifyConfig {
    bool enabled = true;
    MessageType messageType = MessageType::Chat;
    std::string defaultMessage = "{prefix}{RED}{restart_in_seconds}";
    OrderedPairs thresholds;  // секунды строкой -> шаблон

    // Точная отсечка, иначе DefaultMessage; nullptr — шаблона нет вовсе.
    // «Ближайший» порог намеренно не подбирается: на 4 секундах показать
    // «через 5 секунд» так же неверно, как «сервер перезапускается».
    const std::string* ResolveTemplate(int seconds) const;
};

struct Advertisement {
    float interval = 0.0f;
    // Каждое сообщение блока — пары (канал, текст) в порядке файла
    std::vector<OrderedPairs> messages;

    // Следующее сообщение блока по кругу; nullptr — блок пуст.
    const OrderedPairs* NextMessages();

private:
    size_t _next = 0;
};

struct ServerData {
    std::string ip;  // допускается hostname
    int port = 0;
    std::string messageTemplate;
    std::string messageTemplateConsole;
    std::optional<int> maxPlayersFallback;  // на случай OFFLINE
};

struct ServersConfig {
    bool enabled = false;
    float interval = 60.0f;
    std::optional<int> queryTimeoutMs = 500;
    std::optional<int> cacheTtlSeconds = 30;
    std::vector<ServerData> list;
};

using Translations = CiMap<std::string>;  // язык -> текст

struct Config {
    // Settings.json
    bool debug = false;
    std::string defaultLang = "RU";
    std::optional<bool> printToCenterHtml;
    std::optional<float> htmlCenterDuration;
    std::optional<WelcomeMessage> welcomeMessage;
    std::string changeTeamMessage;
    std::string joinTeamMessage;
    std::string titleAnnounceServers;
    std::optional<RestartNotifyConfig> restartNotify;
    OrderedPairs mapsName;
    std::vector<std::pair<std::string, std::vector<std::string>>> languageAliases;

    // Messages.json. Регистр не важен ни у ключей, ни у языков: движок отдаёт "ru",
    // а в конфиге исторически "RU".
    CiMap<Translations> languageMessages;
    CiMap<std::vector<std::string>> joinMessages;
    CiMap<std::vector<std::string>> leaveMessages;

    // Ads.json
    std::vector<Advertisement> ads;

    // Servers.json. Пусто — файла нет или он битый: тогда !servers молчит.
    std::optional<ServersConfig> servers;
};

// Тексты файлов по умолчанию — байт в байт то, что пишет C#-цель при первом
// запуске (config_defaults.cpp). Схемы и README перезаписываются при каждой загрузке.
namespace defaults {
const char* SettingsJson();
const char* MessagesJson();
const char* AdsJson();
const char* ServersJson();
const char* SettingsSchema();
const char* MessagesSchema();
const char* AdsSchema();
const char* ServersSchema();
const char* Readme();
}  // namespace defaults

class ConfigService {
public:
    explicit ConfigService(ILogger* logger) : _logger(logger) {}

    // Читает четыре файла из каталога. Если нет ни одного — создаёт все четыре
    // с примерами. Битый файл не роняет загрузку и НЕ перезаписывается: для него
    // берутся значения по умолчанию, а в лог уходят файл, строка и позиция.
    Config LoadOrCreate(const std::string& directory);

    // Дефолтная конфигурация целиком — ровно та, что пишется при первом запуске.
    static Config BuildDefaultConfig();

private:
    void Validate(const Config& config, const std::string& directory) const;

    ILogger* _logger;
    std::vector<std::string> _failedFiles;
};

}  // namespace nm
