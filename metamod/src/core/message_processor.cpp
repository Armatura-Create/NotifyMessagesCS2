#include "core/message_processor.h"

#include "core/text_formatter.h"

#include <cctype>
#include <cstdint>
#include <ctime>
#include <random>

namespace nm {
namespace {

constexpr const char* kSystemTags[] = {"{MAP}",  "{TIME}", "{DATE}",       "{SERVERNAME}",
                                       "{IP}",   "{PORT}", "{MAXPLAYERS}", "{PLAYERS}"};

constexpr const char* kParagraph = "\xE2\x80\xA9";  // U+2029: перенос строки чата и центра

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

// Кодовая точка UTF-8, начинающаяся в позиции at (или заканчивающаяся перед ней).
// Битая последовательность читается как один байт — для границы слова этого хватает.
uint32_t DecodeAt(const std::string& s, size_t at) {
    const unsigned char b0 = static_cast<unsigned char>(s[at]);
    if (b0 < 0x80) return b0;
    const int extra = b0 >= 0xF0 ? 3 : b0 >= 0xE0 ? 2 : b0 >= 0xC0 ? 1 : 0;
    if (extra == 0 || at + extra >= s.size()) return b0;
    uint32_t cp = extra == 3 ? (b0 & 0x07) : extra == 2 ? (b0 & 0x0F) : (b0 & 0x1F);
    for (int i = 1; i <= extra; ++i) {
        const unsigned char b = static_cast<unsigned char>(s[at + i]);
        if ((b & 0xC0) != 0x80) return b0;
        cp = (cp << 6) | (b & 0x3F);
    }
    return cp;
}

uint32_t DecodeBefore(const std::string& s, size_t at) {
    size_t start = at - 1;
    while (start > 0 && (static_cast<unsigned char>(s[start]) & 0xC0) == 0x80 && at - start < 4) {
        --start;
    }
    return DecodeAt(s, start);
}

// Символ «слова» для \b в .NET: буквы, цифры и подчёркивание.
// ponytail: для не-ASCII — эвристика по блокам (пунктуация, стрелки, символы,
// эмодзи — не слово; остальное — слово). Полная таблица Unicode ради границы
// имени карты не нужна; расхождение с C# возможно только у экзотических символов.
bool IsWordChar(uint32_t cp) {
    if (cp < 0x80) return std::isalnum(static_cast<int>(cp)) || cp == '_';
    if (cp <= 0xBF || cp == 0xD7 || cp == 0xF7) return false;       // Latin-1: знаки
    if (cp >= 0x2000 && cp <= 0x2BFF) return false;                  // пунктуация, стрелки, символы
    if (cp >= 0x2E00 && cp <= 0x2E7F) return false;
    if (cp >= 0x3000 && cp <= 0x303F) return false;                  // CJK-пунктуация
    if (cp >= 0xFE30 && cp <= 0xFE4F) return false;
    if (cp >= 0xFF00 && cp <= 0xFF0F) return false;
    if (cp >= 0x1F000 && cp <= 0x1FAFF) return false;                // эмодзи
    return true;
}

// Regex.Replace(text, $@"\b{key}\b", _ => niceName). Замена функцией, а не строкой:
// в строке замены "$" — спецсимвол, и имя карты с долларом превращалось в мусор.
std::string ReplaceWholeWord(const std::string& text, const std::string& key,
                             const std::string& replacement) {
    if (key.empty()) return text;

    std::string out;
    size_t last = 0;
    size_t at = text.find(key);
    while (at != std::string::npos) {
        const size_t end = at + key.size();
        // \b стоит между символом слова и не-словом. Для ключа, начинающегося
        // не с символа слова ("-map"), граница там, где слева слово.
        const bool keyStartsWord = IsWordChar(DecodeAt(key, 0));
        const bool keyEndsWord = IsWordChar(DecodeBefore(key, key.size()));
        const bool leftWord = at > 0 && IsWordChar(DecodeBefore(text, at));
        const bool rightWord = end < text.size() && IsWordChar(DecodeAt(text, end));

        if (leftWord != keyStartsWord && rightWord != keyEndsWord) {
            out.append(text, last, at - last);
            out += replacement;
            last = end;
            at = text.find(key, end);
        } else {
            at = text.find(key, at + 1);
        }
    }
    if (last == 0 && out.empty()) return text;
    out.append(text, last, std::string::npos);
    return out;
}

// Локальное время сервера: {TIME} и {DATE} в C# идут от DateTime.Now
std::string FormatNow(const char* format) {
    const std::time_t now = std::time(nullptr);
    std::tm local{};
#ifdef _WIN32
    localtime_s(&local, &now);
#else
    localtime_r(&now, &local);
#endif
    char buffer[32];
    const size_t size = std::strftime(buffer, sizeof(buffer), format, &local);
    return std::string(buffer, size);
}

std::mt19937& Random() {
    // Один генератор на процесс; зовётся только из главного потока
    static std::mt19937 generator{std::random_device{}()};
    return generator;
}

}  // namespace

bool MessageProcessor::IsSystemTag(const std::string& tag) {
    for (const char* system : kSystemTags) {
        if (EqualsIgnoreCase(tag, system)) return true;
    }
    return false;
}

std::vector<TagMatch> MessageProcessor::FindTags(const std::string& text) {
    std::vector<TagMatch> matches;
    size_t from = 0;
    while (true) {
        const size_t open = text.find('{', from);
        if (open == std::string::npos) break;
        const size_t close = text.find('}', open + 1);
        if (close == std::string::npos) break;
        matches.push_back({text.substr(open, close - open + 1), text.substr(open + 1, close - open - 1)});
        from = close + 1;
    }
    return matches;
}

std::string MessageProcessor::ProcessMessage(const std::string& message, uint64_t steamId,
                                             MessageType channel, const Values* values) const {
    if (message.empty()) return std::string();

    std::string result = ApplyLanguage(message, steamId);
    result = ApplyValues(result, values, channel);
    result = ReplaceMessageTags(result);
    return Render(result, channel);
}

std::string MessageProcessor::ApplyLanguage(const std::string& message, uint64_t steamId) const {
    if (_config->languageMessages.empty()) return message;

    std::string result = message;
    for (const TagMatch& match : FindTags(message)) {
        const auto language = _config->languageMessages.find(match.name);
        if (language == _config->languageMessages.end()) continue;

        const std::string iso = steamId > 0 ? _language(steamId) : _config->defaultLang;

        const auto exact = language->second.find(iso);
        if (exact != language->second.end()) {
            result = text::ReplaceOrdinal(result, match.tag, exact->second);
            continue;
        }

        const auto fallback = language->second.find(_config->defaultLang);
        if (fallback != language->second.end()) {
            result = text::ReplaceOrdinal(result, match.tag, fallback->second);
        }
    }
    return result;
}

std::string MessageProcessor::ApplyValues(const std::string& message, const Values* values,
                                          MessageType channel) {
    if (values == nullptr || values->empty()) return message;

    // Регистр игнорируется: дефолтный Messages.json пишет часть тегов строчными
    std::string result = message;
    for (const auto& value : *values) {
        const std::string safe =
            channel == MessageType::CenterHtml ? text::EscapeHtml(value.second) : value.second;
        result = text::ReplaceIgnoreCase(result, value.first, safe);
    }
    return result;
}

std::string MessageProcessor::Render(const std::string& input, MessageType channel) {
    switch (channel) {
        case MessageType::Chat:
            return text::ReplaceOrdinal(text::ReplaceColorTags(input), "\n", kParagraph);
        case MessageType::CenterHtml:
            return text::ToCenterHtml(input);
        case MessageType::Console:
            // В консоли перенос строки — настоящий \n, а не U+2029
            return text::RemoveColorTags(input);
        default:
            return text::ReplaceOrdinal(text::RemoveColorTags(input), "\n", kParagraph);
    }
}

std::string MessageProcessor::GetRandomLocalizedMessage(
    const CiMap<std::vector<std::string>>& messages, uint64_t recipientSteamId) const {
    if (messages.empty()) return std::string();

    std::string lang = _config->defaultLang;
    const std::string iso = _language(recipientSteamId);
    if (!iso.empty() && messages.count(iso) != 0) lang = iso;

    const auto list = messages.find(lang);
    if (list == messages.end() || list->second.empty()) return std::string();

    std::uniform_int_distribution<size_t> pick(0, list->second.size() - 1);
    return list->second[pick(Random())];
}

std::string MessageProcessor::ReplaceMessageTags(const std::string& message) const {
    if (message.empty()) return message;

    std::string result = message;

    if (result.find("{MAP}") != std::string::npos) {
        result = text::ReplaceOrdinal(result, "{MAP}", _server->MapName());
    }
    if (result.find("{TIME}") != std::string::npos) {
        result = text::ReplaceOrdinal(result, "{TIME}", FormatNow("%H:%M:%S"));
    }
    if (result.find("{DATE}") != std::string::npos) {
        result = text::ReplaceOrdinal(result, "{DATE}", FormatNow("%d.%m.%Y"));
    }
    if (result.find("{SERVERNAME}") != std::string::npos) {
        result = text::ReplaceOrdinal(result, "{SERVERNAME}", _server->Hostname());
    }
    if (result.find("{IP}") != std::string::npos) {
        result = text::ReplaceOrdinal(result, "{IP}", _server->Ip());
    }
    if (result.find("{PORT}") != std::string::npos) {
        result = text::ReplaceOrdinal(result, "{PORT}", _server->Port());
    }
    if (result.find("{MAXPLAYERS}") != std::string::npos) {
        result = text::ReplaceOrdinal(result, "{MAXPLAYERS}", std::to_string(_server->MaxPlayers()));
    }
    if (result.find("{PLAYERS}") != std::string::npos) {
        result = text::ReplaceOrdinal(result, "{PLAYERS}", std::to_string(_server->Players()));
    }

    for (const auto& map : _config->mapsName) {
        if (result.find(map.first) != std::string::npos) {
            result = ReplaceWholeWord(result, map.first, map.second);
        }
    }

    return result;
}

}  // namespace nm
