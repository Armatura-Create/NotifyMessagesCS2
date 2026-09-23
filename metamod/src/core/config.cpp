#include "core/config.h"

#include "core/logger.h"
#include "core/template_diagnostics.h"
#include "core/util/fs.h"

#include <nlohmann/json.hpp>

#include <cctype>
#include <cstdlib>
#include <fstream>
#include <functional>
#include <sstream>
#include <sys/stat.h>

namespace nm {

bool CaseInsensitiveLess::operator()(const std::string& a, const std::string& b) const {
    const size_t n = a.size() < b.size() ? a.size() : b.size();
    for (size_t i = 0; i < n; ++i) {
        const int x = std::tolower(static_cast<unsigned char>(a[i]));
        const int y = std::tolower(static_cast<unsigned char>(b[i]));
        if (x != y) return x < y;
    }
    return a.size() < b.size();
}

namespace {

constexpr const char* kTypeNames[] = {"Chat", "Center", "CenterHtml", "Console", "Alert"};

bool EqualsIgnoreCase(const std::string& a, const char* b) {
    size_t i = 0;
    for (; i < a.size() && b[i] != '\0'; ++i) {
        if (std::tolower(static_cast<unsigned char>(a[i])) !=
            std::tolower(static_cast<unsigned char>(b[i]))) {
            return false;
        }
    }
    return i == a.size() && b[i] == '\0';
}

// Число вне 0..4 C# принимает как есть, и в switch по каналу такое значение уходит
// в ветку по умолчанию — обычный центр экрана. Здесь сразу Center.
MessageType FromNumber(int64_t value) {
    return value >= 0 && value <= 4 ? static_cast<MessageType>(value) : MessageType::Center;
}

}  // namespace

bool ParseMessageType(const std::string& text, MessageType* out) {
    size_t begin = 0;
    size_t end = text.size();
    while (begin < end && std::isspace(static_cast<unsigned char>(text[begin]))) ++begin;
    while (end > begin && std::isspace(static_cast<unsigned char>(text[end - 1]))) --end;
    const std::string value = text.substr(begin, end - begin);
    if (value.empty()) return false;

    for (int i = 0; i < 5; ++i) {
        if (EqualsIgnoreCase(value, kTypeNames[i])) {
            *out = static_cast<MessageType>(i);
            return true;
        }
    }

    for (const char c : value) {
        if (!std::isdigit(static_cast<unsigned char>(c))) return false;
    }
    if (value.size() > 9) return false;
    *out = FromNumber(std::strtol(value.c_str(), nullptr, 10));
    return true;
}

const char* MessageTypeName(MessageType type) {
    const int index = static_cast<int>(type);
    return index >= 0 && index <= 4 ? kTypeNames[index] : "Center";
}

const std::string* RestartNotifyConfig::ResolveTemplate(int seconds) const {
    const std::string key = std::to_string(seconds);
    for (const auto& threshold : thresholds) {
        if (threshold.first == key && !threshold.second.empty()) return &threshold.second;
    }
    return defaultMessage.empty() ? nullptr : &defaultMessage;
}

const OrderedPairs* Advertisement::NextMessages() {
    if (messages.empty()) return nullptr;

    // Индекс сбрасывается сам: в C#-цели он когда-то рос без границ и после
    // переполнения давал отрицательный остаток
    if (_next >= messages.size()) _next = 0;
    return &messages[_next++];
}

namespace {

using Json = nlohmann::ordered_json;

// Чтение значений с проверкой типов.
//
// System.Text.Json в C#-целях отбрасывает ВЕСЬ файл, если хоть одно поле имеет
// не тот тип ("Interval": "abc", "Debug": null). Здесь то же поведение: ошибки
// копятся, и при любой из них файл целиком заменяется значениями по умолчанию.
// Иначе один и тот же битый конфиг работал бы на разных целях по-разному.
//
// Исключений нет сознательно: игровая сборка идёт с -fno-exceptions, и
// nlohmann/json в таком режиме на ошибке зовёт std::abort(). Поэтому тип
// проверяется заранее, а get<T> вызывается только когда он заведомо подойдёт.
class Reader {
public:
    std::vector<std::string> errors;

    const Json* Find(const Json& node, const char* key) const {
        if (!node.is_object()) return nullptr;
        const auto it = node.find(key);
        return it == node.end() ? nullptr : &*it;
    }

    void Fail(const std::string& path, const std::string& problem) {
        errors.push_back("поле " + path + " — " + problem);
    }

    void Bool(const Json& node, const char* key, const std::string& path, bool* out) {
        const Json* value = Find(node, key);
        if (value == nullptr) return;
        if (value->is_boolean()) {
            *out = value->get<bool>();
        } else {
            Fail(path, "ожидалось true или false");
        }
    }

    void OptBool(const Json& node, const char* key, const std::string& path,
                 std::optional<bool>* out) {
        const Json* value = Find(node, key);
        if (value == nullptr) return;
        if (value->is_null()) {
            out->reset();
        } else if (value->is_boolean()) {
            *out = value->get<bool>();
        } else {
            Fail(path, "ожидалось true, false или null");
        }
    }

    void Float(const Json& node, const char* key, const std::string& path, float* out) {
        const Json* value = Find(node, key);
        if (value == nullptr) return;
        if (value->is_number()) {
            *out = value->get<float>();
        } else {
            Fail(path, "ожидалось число");
        }
    }

    void OptFloat(const Json& node, const char* key, const std::string& path,
                  std::optional<float>* out) {
        const Json* value = Find(node, key);
        if (value == nullptr) return;
        if (value->is_null()) {
            out->reset();
        } else if (value->is_number()) {
            *out = value->get<float>();
        } else {
            Fail(path, "ожидалось число или null");
        }
    }

    // Целое в пределах int32: 5.0 C# тоже не примет за int
    bool IntValue(const Json& value, int* out) {
        if (!value.is_number_integer()) return false;
        if (value.is_number_unsigned()) {
            const uint64_t raw = value.get<uint64_t>();
            if (raw > 2147483647ull) return false;
            *out = static_cast<int>(raw);
            return true;
        }
        const int64_t raw = value.get<int64_t>();
        if (raw < -2147483647ll - 1 || raw > 2147483647ll) return false;
        *out = static_cast<int>(raw);
        return true;
    }

    void Int(const Json& node, const char* key, const std::string& path, int* out) {
        const Json* value = Find(node, key);
        if (value == nullptr) return;
        if (!IntValue(*value, out)) Fail(path, "ожидалось целое число");
    }

    void OptInt(const Json& node, const char* key, const std::string& path,
                std::optional<int>* out) {
        const Json* value = Find(node, key);
        if (value == nullptr) return;
        if (value->is_null()) {
            out->reset();
            return;
        }
        int number = 0;
        if (IntValue(*value, &number)) {
            *out = number;
        } else {
            Fail(path, "ожидалось целое число или null");
        }
    }

    // Строка в C# — ссылочный тип: null допустим и значит «не задано».
    // isNull сообщает вызывающему, что там был именно null.
    void String(const Json& node, const char* key, const std::string& path, std::string* out,
                bool* isNull = nullptr) {
        const Json* value = Find(node, key);
        if (value == nullptr) return;
        if (value->is_null()) {
            out->clear();
            if (isNull != nullptr) *isNull = true;
        } else if (value->is_string()) {
            *out = value->get<std::string>();
        } else {
            Fail(path, "ожидалась строка");
        }
    }

    // "Chat" / "centerhtml" / 2 — как JsonStringEnumConverter
    void Type(const Json& node, const char* key, const std::string& path, MessageType* out) {
        const Json* value = Find(node, key);
        if (value == nullptr) return;
        if (value->is_string()) {
            if (!ParseMessageType(value->get<std::string>(), out)) {
                Fail(path, "неизвестный канал \"" + value->get<std::string>() +
                               "\". Допустимые: Chat, Center, CenterHtml, Console, Alert");
            }
        } else if (value->is_number_integer()) {
            *out = FromNumber(value->is_number_unsigned()
                                  ? static_cast<int64_t>(value->get<uint64_t>() & 0x7FFFFFFF)
                                  : value->get<int64_t>());
        } else {
            Fail(path, "ожидалось имя канала: Chat, Center, CenterHtml, Console, Alert");
        }
    }

    // Объект или null. false — поля нет или там null; ошибка типа уже записана.
    const Json* Object(const Json& node, const char* key, const std::string& path) {
        const Json* value = Find(node, key);
        if (value == nullptr || value->is_null()) return nullptr;
        if (!value->is_object()) {
            Fail(path, "ожидался объект { ... }");
            return nullptr;
        }
        return value;
    }

    const Json* Array(const Json& node, const char* key, const std::string& path) {
        const Json* value = Find(node, key);
        if (value == nullptr || value->is_null()) return nullptr;
        if (!value->is_array()) {
            Fail(path, "ожидался массив [ ... ]");
            return nullptr;
        }
        return value;
    }

    // Объект "ключ": "строка" (или null). Порядок файла сохраняется.
    void StringMap(const Json& node, const char* key, const std::string& path, OrderedPairs* out) {
        const Json* object = Object(node, key, path);
        if (object == nullptr) return;
        for (auto it = object->begin(); it != object->end(); ++it) {
            if (it.value().is_string()) {
                out->emplace_back(it.key(), it.value().get<std::string>());
            } else if (it.value().is_null()) {
                out->emplace_back(it.key(), std::string());
            } else {
                Fail(path + "." + it.key(), "ожидалась строка");
            }
        }
    }

    // Массив строк; null внутри — пустая строка, как null в List<string>
    void StringList(const Json& value, const std::string& path, std::vector<std::string>* out) {
        if (value.is_null()) return;
        if (!value.is_array()) {
            Fail(path, "ожидался массив строк");
            return;
        }
        for (const Json& item : value) {
            if (item.is_string()) {
                out->push_back(item.get<std::string>());
            } else if (item.is_null()) {
                out->push_back(std::string());
            } else {
                Fail(path, "ожидался массив строк");
                return;
            }
        }
    }
};

void ReadSettings(const Json& root, Reader& r, Config* c) {
    r.Bool(root, "Debug", "Debug", &c->debug);

    // null в DefaultLang — это ?? "RU" в MergeParts, а пустая строка остаётся пустой
    bool langNull = false;
    r.String(root, "DefaultLang", "DefaultLang", &c->defaultLang, &langNull);
    if (langNull) c->defaultLang = "RU";

    r.OptBool(root, "PrintToCenterHtml", "PrintToCenterHtml", &c->printToCenterHtml);

    // Читается ради проверки типа, но в этой цели не действует: пауза HTML-центра
    // на время смерти требует читать поле пешки, то есть смещения движка.
    std::optional<bool> showWhenDead;
    r.OptBool(root, "ShowHtmlWhenDead", "ShowHtmlWhenDead", &showWhenDead);

    r.OptFloat(root, "HtmlCenterDuration", "HtmlCenterDuration", &c->htmlCenterDuration);

    if (const Json* welcome = r.Object(root, "WelcomeMessage", "WelcomeMessage")) {
        WelcomeMessage value;
        r.Type(*welcome, "MessageType", "WelcomeMessage.MessageType", &value.messageType);
        // required в модели C#: без Message весь Settings.json не читается
        if (r.Find(*welcome, "Message") == nullptr) {
            r.Fail("WelcomeMessage.Message", "обязательное поле отсутствует");
        }
        r.String(*welcome, "Message", "WelcomeMessage.Message", &value.message);
        r.Float(*welcome, "DisplayDelay", "WelcomeMessage.DisplayDelay", &value.displayDelay);
        c->welcomeMessage = value;
    }

    r.String(root, "ChangeTeamMessage", "ChangeTeamMessage", &c->changeTeamMessage);
    r.String(root, "JoinTeamMessage", "JoinTeamMessage", &c->joinTeamMessage);
    r.String(root, "TitleAnnounceServers", "TitleAnnounceServers", &c->titleAnnounceServers);

    if (const Json* notify = r.Object(root, "RestartNotify", "RestartNotify")) {
        RestartNotifyConfig value;
        r.Bool(*notify, "Enabled", "RestartNotify.Enabled", &value.enabled);
        r.Type(*notify, "MessageType", "RestartNotify.MessageType", &value.messageType);
        r.String(*notify, "DefaultMessage", "RestartNotify.DefaultMessage", &value.defaultMessage);
        r.StringMap(*notify, "Thresholds", "RestartNotify.Thresholds", &value.thresholds);
        c->restartNotify = value;
    }

    r.StringMap(root, "MapsName", "MapsName", &c->mapsName);

    if (const Json* aliases = r.Object(root, "LanguageAliases", "LanguageAliases")) {
        for (auto it = aliases->begin(); it != aliases->end(); ++it) {
            std::vector<std::string> codes;
            r.StringList(it.value(), "LanguageAliases." + it.key(), &codes);
            c->languageAliases.emplace_back(it.key(), codes);
        }
    }
}

void ReadMessages(const Json& root, Reader& r, Config* c) {
    if (const Json* messages = r.Object(root, "LanguageMessages", "LanguageMessages")) {
        for (auto key = messages->begin(); key != messages->end(); ++key) {
            const std::string path = "LanguageMessages." + key.key();
            Translations translations;
            if (key.value().is_object()) {
                for (auto lang = key.value().begin(); lang != key.value().end(); ++lang) {
                    if (lang.value().is_string()) {
                        translations[lang.key()] = lang.value().get<std::string>();
                    } else if (lang.value().is_null()) {
                        translations[lang.key()] = std::string();
                    } else {
                        r.Fail(path + "." + lang.key(), "ожидалась строка");
                    }
                }
            } else if (!key.value().is_null()) {
                r.Fail(path, "ожидался объект язык -> текст");
            }
            c->languageMessages[key.key()] = translations;
        }
    }

    const auto readLists = [&r](const Json& node, const char* name, CiMap<std::vector<std::string>>* out) {
        const Json* lists = r.Object(node, name, name);
        if (lists == nullptr) return;
        for (auto it = lists->begin(); it != lists->end(); ++it) {
            std::vector<std::string> list;
            r.StringList(it.value(), std::string(name) + "." + it.key(), &list);
            (*out)[it.key()] = list;
        }
    };
    readLists(root, "JoinMessages", &c->joinMessages);
    readLists(root, "LeaveMessages", &c->leaveMessages);
}

void ReadAds(const Json& root, Reader& r, Config* c) {
    const Json* ads = r.Array(root, "Ads", "Ads");
    if (ads == nullptr) return;

    for (size_t i = 0; i < ads->size(); ++i) {
        const Json& item = (*ads)[i];
        const std::string path = "Ads[" + std::to_string(i) + "]";
        if (item.is_null()) continue;
        if (!item.is_object()) {
            r.Fail(path, "ожидался объект { Interval, Messages }");
            continue;
        }

        Advertisement ad;
        r.Float(item, "Interval", path + ".Interval", &ad.interval);
        if (const Json* messages = r.Array(item, "Messages", path + ".Messages")) {
            for (size_t m = 0; m < messages->size(); ++m) {
                const Json& block = (*messages)[m];
                const std::string blockPath = path + ".Messages[" + std::to_string(m) + "]";
                OrderedPairs channels;
                if (block.is_object()) {
                    for (auto it = block.begin(); it != block.end(); ++it) {
                        if (it.value().is_string()) {
                            channels.emplace_back(it.key(), it.value().get<std::string>());
                        } else if (it.value().is_null()) {
                            channels.emplace_back(it.key(), std::string());
                        } else {
                            r.Fail(blockPath + "." + it.key(), "ожидалась строка");
                        }
                    }
                } else if (!block.is_null()) {
                    r.Fail(blockPath, "ожидался объект канал -> текст");
                }
                ad.messages.push_back(channels);
            }
        }
        c->ads.push_back(ad);
    }
}

void ReadServers(const Json& root, Reader& r, Config* c) {
    ServersConfig servers;
    r.Bool(root, "Enabled", "Enabled", &servers.enabled);
    r.Float(root, "Interval", "Interval", &servers.interval);
    r.OptInt(root, "QueryTimeoutMs", "QueryTimeoutMs", &servers.queryTimeoutMs);
    r.OptInt(root, "CacheTtlSeconds", "CacheTtlSeconds", &servers.cacheTtlSeconds);

    if (const Json* list = r.Array(root, "List", "List")) {
        for (size_t i = 0; i < list->size(); ++i) {
            const Json& item = (*list)[i];
            const std::string path = "List[" + std::to_string(i) + "]";
            if (item.is_null()) continue;
            if (!item.is_object()) {
                r.Fail(path, "ожидался объект { Ip, Port, ... }");
                continue;
            }

            ServerData server;
            r.String(item, "Ip", path + ".Ip", &server.ip);
            r.Int(item, "Port", path + ".Port", &server.port);
            r.String(item, "MessageTemplate", path + ".MessageTemplate", &server.messageTemplate);
            r.String(item, "MessageTemplateConsole", path + ".MessageTemplateConsole",
                     &server.messageTemplateConsole);
            r.OptInt(item, "MaxPlayersFallback", path + ".MaxPlayersFallback",
                     &server.maxPlayersFallback);
            servers.list.push_back(server);
        }
    }

    c->servers = servers;
}

// Первый отказ разбора: позиция и текст. Двухпроходная схема (сначала SAX ради
// позиции, потом обычный разбор) — потому что без исключений parse() про место
// ошибки не говорит ничего.
class ErrorLocator final : public nlohmann::json_sax<Json> {
public:
    size_t position = 0;
    std::string message;

    bool null() override { return true; }
    bool boolean(bool) override { return true; }
    bool number_integer(number_integer_t) override { return true; }
    bool number_unsigned(number_unsigned_t) override { return true; }
    bool number_float(number_float_t, const string_t&) override { return true; }
    bool string(string_t&) override { return true; }
    bool binary(binary_t&) override { return true; }
    bool start_object(std::size_t) override { return true; }
    bool key(string_t&) override { return true; }
    bool end_object() override { return true; }
    bool start_array(std::size_t) override { return true; }
    bool end_array() override { return true; }

    bool parse_error(std::size_t pos, const std::string&, const nlohmann::detail::exception& ex) override {
        position = pos;
        message = ex.what();
        return false;
    }
};

// Висячие запятые и //-комментарии разрешены осознанно, как в ReadOptions C#:
// это самые частые «ошибки» в конфигах, а данные из них читаются однозначно.
bool Parse(const std::string& text, Json* out, std::string* error) {
    ErrorLocator locator;
    if (!Json::sax_parse(text, &locator, nlohmann::detail::input_format_t::json, true, true, true)) {
        // Позиция ошибки — номер байта; строка и позиция в ней — как у C# (с нуля)
        const size_t at = locator.position > 0 ? locator.position - 1 : 0;
        size_t line = 1;
        size_t lineStart = 0;
        for (size_t i = 0; i < at && i < text.size(); ++i) {
            if (text[i] == '\n') {
                ++line;
                lineStart = i + 1;
            }
        }
        *error = "строка " + std::to_string(line) + ", позиция " +
                 std::to_string(at >= lineStart ? at - lineStart : 0) + ". Подробности: " +
                 locator.message;
        return false;
    }

    *out = Json::parse(text, nullptr, false, true, true);
    if (out->is_discarded()) {
        *error = "разбор не удался";
        return false;
    }
    return true;
}

enum class Part { Settings, Messages, Ads, Servers };

void ReadPart(Part part, const Json& root, Reader& r, Config* c) {
    switch (part) {
        case Part::Settings: ReadSettings(root, r, c); break;
        case Part::Messages: ReadMessages(root, r, c); break;
        case Part::Ads: ReadAds(root, r, c); break;
        case Part::Servers: ReadServers(root, r, c); break;
    }
}

std::string StripBom(const std::string& text) {
    return text.compare(0, 3, "\xEF\xBB\xBF") == 0 ? text.substr(3) : text;
}

bool IsBlank(const std::string& text) {
    for (const char ch : text) {
        if (!std::isspace(static_cast<unsigned char>(ch))) return false;
    }
    return true;
}

bool ReadWholeFile(const std::string& path, std::string* out) {
    std::ifstream stream(path.c_str(), std::ios::binary);
    if (!stream.is_open()) return false;
    std::ostringstream buffer;
    buffer << stream.rdbuf();
    *out = buffer.str();
    return !stream.bad();
}

// С BOM, как File.WriteAllText(path, text, Encoding.UTF8) в C#: файлы, созданные
// разными целями, должны совпадать байт в байт.
bool WriteWithBom(const std::string& path, const char* content) {
    std::ofstream stream(path.c_str(), std::ios::binary | std::ios::trunc);
    if (!stream.is_open()) return false;
    stream << "\xEF\xBB\xBF" << content;
    return stream.good();
}

bool FileExists(const std::string& path) {
    struct stat info;
    return stat(path.c_str(), &info) == 0;
}

std::string Join(const std::string& directory, const char* name) {
    if (directory.empty()) return name;
    const char last = directory.back();
    return (last == '/' || last == '\\') ? directory + name : directory + "/" + name;
}

struct FileSpec {
    const char* name;
    Part part;
    const char* (*defaults)();
    const char* schemaName;
    const char* (*schema)();
};

constexpr FileSpec kFiles[] = {
    {"Settings.json", Part::Settings, defaults::SettingsJson, "Settings.schema.json", defaults::SettingsSchema},
    {"Messages.json", Part::Messages, defaults::MessagesJson, "Messages.schema.json", defaults::MessagesSchema},
    {"Ads.json", Part::Ads, defaults::AdsJson, "Ads.schema.json", defaults::AdsSchema},
    {"Servers.json", Part::Servers, defaults::ServersJson, "Servers.schema.json", defaults::ServersSchema},
};

// Текст одного файла -> часть конфига. false — файл отвергнут целиком,
// причина уже в *error.
bool ApplyText(Part part, const std::string& text, Config* config, std::string* error) {
    Json root;
    if (!Parse(text, &root, error)) return false;

    if (root.is_null()) {
        *error = "файл содержит null вместо объекта";
        return false;
    }
    if (!root.is_object()) {
        *error = "ожидался объект { ... } на верхнем уровне";
        return false;
    }

    // Копия принимается целиком или не принимается: полуприменённый файл —
    // это конфиг, которого никто не писал
    Config candidate = *config;
    Reader reader;
    ReadPart(part, root, reader, &candidate);
    if (!reader.errors.empty()) {
        *error = reader.errors.front();
        return false;
    }

    *config = candidate;
    return true;
}

}  // namespace

Config ConfigService::BuildDefaultConfig() {
    Config config;
    std::string error;
    for (const FileSpec& file : kFiles) ApplyText(file.part, file.defaults(), &config, &error);
    return config;
}

Config ConfigService::LoadOrCreate(const std::string& directory) {
    _failedFiles.clear();

    if (!fs::EnsureDirectory(directory)) {
        if (_logger != nullptr) {
            _logger->Error("[Load] Конфигурацию загрузить не удалось — не создаётся каталог " +
                           directory + ". Плагин стартует с пустыми настройками и ничего "
                           "показывать не будет. Выполните mm_reload_advert после исправления");
        }
        return Config();
    }

    bool anyExists = false;
    for (const FileSpec& file : kFiles) anyExists = anyExists || FileExists(Join(directory, file.name));

    // Схемы и README перезаписываются всегда: иначе после обновления плагина
    // они продолжают описывать старую версию и врут админу. Справочные файлы —
    // не повод сорвать загрузку конфига: каталог может быть только для чтения.
    bool referenceOk = WriteWithBom(Join(directory, "README.txt"), defaults::Readme());
    for (const FileSpec& file : kFiles) {
        referenceOk = WriteWithBom(Join(directory, file.schemaName), file.schema()) && referenceOk;
    }
    if (!referenceOk && _logger != nullptr) {
        _logger->Error("[Config] Не удалось обновить README.txt/*.schema.json в " + directory +
                       ". Сама конфигурация читается как обычно");
    }

    if (!anyExists) {
        if (_logger != nullptr) {
            _logger->Info("═══════════════════════════════════════════════════════════════");
            _logger->Info("  NotifyMessages - First Run Detected!");
            _logger->Info("  Creating default configuration files...");
            _logger->Info("═══════════════════════════════════════════════════════════════");
        }
        for (const FileSpec& file : kFiles) {
            const bool written = WriteWithBom(Join(directory, file.name), file.defaults());
            if (_logger != nullptr) {
                _logger->Info(written ? std::string("✓ ") + file.name + " created"
                                      : std::string("✗ ") + file.name + " не записан в " + directory);
            }
        }
        if (_logger != nullptr) {
            _logger->Info("✓ README.txt и *.schema.json созданы");
            _logger->Info("═══════════════════════════════════════════════════════════════");
        }
        return BuildDefaultConfig();
    }

    Config config;
    for (const FileSpec& file : kFiles) {
        const std::string path = Join(directory, file.name);

        if (!FileExists(path)) {
            if (_logger != nullptr) {
                _logger->Info(std::string("[Config] ") + file.name +
                              " не найден — используются значения по умолчанию");
            }
            continue;
        }

        std::string raw;
        if (!ReadWholeFile(path, &raw)) {
            _failedFiles.push_back(file.name);
            if (_logger != nullptr) {
                _logger->Error(std::string("[Config] ") + file.name + ": не удалось прочитать файл " +
                               path + ". Проверьте права доступа. Используются значения по умолчанию");
            }
            continue;
        }

        const std::string text = StripBom(raw);
        if (IsBlank(text)) {
            _failedFiles.push_back(file.name);
            if (_logger != nullptr) {
                _logger->Error(std::string("[Config] ") + file.name + " пуст (" + path +
                               "). Используются значения по умолчанию");
            }
            continue;
        }

        std::string error;
        if (!ApplyText(file.part, text, &config, &error)) {
            _failedFiles.push_back(file.name);
            if (_logger != nullptr) {
                _logger->Error(std::string("[Config] ") + file.name + ": ошибка в JSON — " + error +
                               ". Файл: " + path + ". Весь файл проигнорирован, используются "
                               "значения по умолчанию. Проверьте синтаксис (лишняя/пропущенная "
                               "запятая, кавычки, скобки)");
            }
        }
    }

    Validate(config, directory);
    return config;
}

void ConfigService::Validate(const Config& config, const std::string& directory) const {
    if (_logger == nullptr) return;

    std::vector<std::string> warnings;
    std::vector<std::string> info;

    if (config.defaultLang.empty()) warnings.push_back("DefaultLang not set, using 'RU' as default");
    if (config.languageMessages.empty()) warnings.push_back("No LanguageMessages found in Messages.json");
    if (!config.ads.empty()) {
        info.push_back("Loaded " + std::to_string(config.ads.size()) + " advertisement block(s)");
    }
    if (config.servers && config.servers->enabled) {
        if (config.servers->list.empty()) {
            warnings.push_back("Servers enabled but List is empty");
        } else {
            info.push_back("Loaded " + std::to_string(config.servers->list.size()) +
                           " server(s) for status checking");
        }
    }

    if (!_failedFiles.empty()) {
        _logger->Info("===============================================================");
        _logger->Info("  ВНИМАНИЕ: не удалось прочитать " + std::to_string(_failedFiles.size()) +
                      " файл(ов) конфигурации:");
        for (const std::string& file : _failedFiles) _logger->Info("    - " + file);
        _logger->Info("  Для них взяты значения по умолчанию. Смотрите строки [ERROR] выше:");
        _logger->Info("  там указаны файл, строка и позиция ошибки.");
        _logger->Info("  Каталог конфигов: " + directory);
        _logger->Info("  Починив файлы, примените их командой mm_reload_advert.");
        _logger->Info("===============================================================");
    }

    if (!warnings.empty()) {
        _logger->Info("⚠ Configuration Warnings:");
        for (const std::string& warning : warnings) _logger->Info("  ⚠ " + warning);
    }
    for (const std::string& line : info) _logger->Info("  ℹ " + line);

    // Неизвестный тег доезжает до игрока текстом в скобках, поэтому это Error
    int errors = 0;
    for (const TemplateIssue& issue : CollectIssues(config)) {
        if (issue.severity == TemplateSeverity::Error) {
            ++errors;
            _logger->Error("[Config] " + issue.ToString());
        } else {
            _logger->Info("[Config] " + issue.ToString());
        }
    }
    if (errors > 0) {
        _logger->Info("  ⚠ Шаблонов с неизвестными тегами: " + std::to_string(errors) +
                      ". Проверьте командой mm_nm_check, посмотрите результат командой mm_nm_preview.");
    }

    _logger->Info("✓ Configuration loaded from: " + directory);
}

}  // namespace nm
