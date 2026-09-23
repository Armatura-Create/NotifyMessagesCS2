#include "core/text_formatter.h"

#include <cctype>
#include <cstring>

namespace nm {
namespace text {
namespace {

// Широкий пробел (Hangul filler, U+3164) для выравнивания — тег {SPACE}
constexpr const char* kSpaceFiller = "\xE3\x85\xA4\xE3\x85\xA4\xE3\x85\xA4";

struct ColorTag {
    const char* tag;
    char code;         // управляющий байт чата
    const char* hex;   // приблизительный цвет для HTML-центра
};

// Порядок — как ColorTagMap в C#. Коды — ChatColors CounterStrikeSharp.
// hex — отдельная таблица HtmlColorMap: она НЕ заменяет коды чата.
constexpr ColorTag kColors[] = {
    {"{DEFAULT}", '\x01', "#FFFFFF"},     {"{WHITE}", '\x01', "#FFFFFF"},
    {"{DARKRED}", '\x02', "#8B0000"},     {"{LIGHTYELLOW}", '\x09', "#FFFF99"},
    {"{LIGHTBLUE}", '\x0B', "#99CCFF"},   {"{OLIVE}", '\x05', "#9EC34F"},
    {"{LIME}", '\x06', "#00FF00"},        {"{GREEN}", '\x04', "#3EFF3E"},
    {"{RED}", '\x07', "#FF4040"},         {"{LIGHTPURPLE}", '\x03', "#FF99FF"},
    {"{PURPLE}", '\x0E', "#8B008B"},      {"{GREY}", '\x08', "#CCCCCC"},
    {"{GRAY}", '\x08', "#CCCCCC"},        {"{YELLOW}", '\x09', "#FFFF00"},
    {"{GOLD}", '\x10', "#FFD700"},        {"{SILVER}", '\x0A', "#C0C0C0"},
    {"{BLUE}", '\x0B', "#6699FF"},        {"{DARKBLUE}", '\x0C', "#00008B"},
    {"{BLUEGREY}", '\x0A', "#6A5ACD"},    {"{MAGENTA}", '\x0E', "#FF00FF"},
    {"{LIGHTRED}", '\x0F', "#FF6666"},    {"{ORANGE}", '\x10', "#FFA500"},
};

// Размеры умеет только HTML-панель: классы движка. Незнакомый класс панель
// игнорирует, поэтому худший случай — обычный размер, а не поломанная разметка.
struct SizeTag {
    const char* tag;
    const char* html;
};

constexpr SizeTag kSizes[] = {
    {"{BIG}", "<font class='fontSize-l'>"},
    {"{MEDIUM}", "<font class='fontSize-m'>"},
    {"{SMALL}", "<font class='fontSize-sm'>"},
};

constexpr const char* kKnownTags[] = {
    "{DEFAULT}", "{WHITE}",  "{DARKRED}", "{LIGHTYELLOW}", "{LIGHTBLUE}", "{OLIVE}",
    "{LIME}",    "{GREEN}",  "{RED}",     "{LIGHTPURPLE}", "{PURPLE}",    "{GREY}",
    "{GRAY}",    "{YELLOW}", "{GOLD}",    "{SILVER}",      "{BLUE}",      "{DARKBLUE}",
    "{BLUEGREY}", "{MAGENTA}", "{LIGHTRED}", "{ORANGE}",   "{SPACE}",     "{BIG}",
    "{MEDIUM}",  "{SMALL}",
};

constexpr size_t kLongestTag = 13;  // {LIGHTYELLOW}

bool EqualsIgnoreCaseAt(const std::string& text, size_t position, const std::string& needle) {
    if (position + needle.size() > text.size()) return false;
    for (size_t i = 0; i < needle.size(); ++i) {
        if (std::tolower(static_cast<unsigned char>(text[position + i])) !=
            std::tolower(static_cast<unsigned char>(needle[i]))) {
            return false;
        }
    }
    return true;
}

size_t FindIgnoreCase(const std::string& text, const std::string& needle, size_t from) {
    if (needle.empty() || needle.size() > text.size()) return std::string::npos;
    for (size_t i = from; i + needle.size() <= text.size(); ++i) {
        if (EqualsIgnoreCaseAt(text, i, needle)) return i;
    }
    return std::string::npos;
}

int CountIgnoreCase(const std::string& text, const std::string& needle) {
    int count = 0;
    size_t at = FindIgnoreCase(text, needle, 0);
    while (at != std::string::npos) {
        ++count;
        at = FindIgnoreCase(text, needle, at + needle.size());
    }
    return count;
}

bool IsColorCode(char c) {
    const unsigned char byte = static_cast<unsigned char>(c);
    return byte >= 0x01 && byte <= 0x10;
}

bool HasColorCode(const std::string& text) {
    for (const char c : text) {
        if (IsColorCode(c)) return true;
    }
    return false;
}

std::string RemoveSizeTags(std::string text) {
    if (text.find('{') == std::string::npos) return text;
    for (const SizeTag& size : kSizes) text = ReplaceIgnoreCase(text, size.tag, std::string());
    return text;
}

}  // namespace

std::string ReplaceIgnoreCase(const std::string& text, const std::string& search,
                              const std::string& replacement) {
    size_t at = FindIgnoreCase(text, search, 0);
    if (at == std::string::npos) return text;

    std::string out;
    out.reserve(text.size());
    size_t last = 0;
    while (at != std::string::npos) {
        out.append(text, last, at - last);
        out += replacement;
        last = at + search.size();
        at = FindIgnoreCase(text, search, last);
    }
    out.append(text, last, std::string::npos);
    return out;
}

std::string ReplaceOrdinal(const std::string& text, const std::string& search,
                           const std::string& replacement) {
    if (search.empty()) return text;
    size_t at = text.find(search);
    if (at == std::string::npos) return text;

    std::string out;
    out.reserve(text.size());
    size_t last = 0;
    while (at != std::string::npos) {
        out.append(text, last, at - last);
        out += replacement;
        last = at + search.size();
        at = text.find(search, last);
    }
    out.append(text, last, std::string::npos);
    return out;
}

bool ContainsIgnoreCase(const std::string& text, const std::string& search) {
    return FindIgnoreCase(text, search, 0) != std::string::npos;
}

// Замена идёт от длинных тегов к коротким, как SortedTags в C#: порядок
// сортировки менять нельзя, это фикс конфликта префиксов тегов.
std::string ReplaceColorTags(const std::string& input) {
    if (input.empty() || input.find('{') == std::string::npos) return input;

    // Ordinal, как input.Replace("{SPACE}", …) в C#: регистр здесь важен
    std::string result = ReplaceOrdinal(input, "{SPACE}", kSpaceFiller);

    for (size_t length = kLongestTag; length > 0; --length) {
        for (const ColorTag& color : kColors) {
            if (std::strlen(color.tag) != length) continue;
            result = ReplaceIgnoreCase(result, color.tag, std::string(1, color.code));
        }
    }

    // Один шаблон может ходить и в чат, и в панель: размер не должен доезжать текстом
    return RemoveSizeTags(result);
}

std::string ToCenterHtml(const std::string& input) {
    if (input.empty()) return input;

    std::string result = ReplaceOrdinal(input, "\n", "<br>");
    result = ReplaceOrdinal(result, "\xE2\x80\xA9", "<br>");  // U+2029
    result = ReplaceIgnoreCase(result, "{SPACE}", "&nbsp;&nbsp;&nbsp;");

    int opened = 0;
    for (size_t length = kLongestTag; length > 0; --length) {
        for (const ColorTag& color : kColors) {
            if (std::strlen(color.tag) != length) continue;
            const int count = CountIgnoreCase(result, color.tag);
            if (count == 0) continue;
            opened += count;
            result = ReplaceIgnoreCase(result, color.tag,
                                       std::string("<font color='") + color.hex + "'>");
        }
        for (const SizeTag& size : kSizes) {
            if (std::strlen(size.tag) != length) continue;
            const int count = CountIgnoreCase(result, size.tag);
            if (count == 0) continue;
            opened += count;
            result = ReplaceIgnoreCase(result, size.tag, size.html);
        }
    }

    for (int i = 0; i < opened; ++i) result += "</font>";
    return result;
}

std::string RemoveColorTags(const std::string& input) {
    if (input.empty() || input.find('{') == std::string::npos) return input;

    std::string result = ReplaceIgnoreCase(input, "{SPACE}", kSpaceFiller);
    for (size_t length = kLongestTag; length > 0; --length) {
        for (const ColorTag& color : kColors) {
            if (std::strlen(color.tag) == length) result = ReplaceIgnoreCase(result, color.tag, "");
        }
        for (const SizeTag& size : kSizes) {
            if (std::strlen(size.tag) == length) result = ReplaceIgnoreCase(result, size.tag, "");
        }
    }
    return result;
}

std::string EscapeHtml(const std::string& input) {
    std::string out;
    out.reserve(input.size());
    for (const char c : input) {
        switch (c) {
            case '&': out += "&amp;"; break;
            case '<': out += "&lt;"; break;
            case '>': out += "&gt;"; break;
            default: out.push_back(c); break;
        }
    }
    return out;
}

std::string StripColorCodes(const std::string& input) {
    std::string out;
    out.reserve(input.size());
    for (const char c : input) {
        if (!IsColorCode(c)) out.push_back(c);
    }
    return out;
}

std::string EnsureChatColorPrefix(const std::string& input) {
    // Без цветов чинить нечего — обычному тексту ведущий пробел ни к чему
    if (input.empty() || !HasColorCode(input)) return input;

    // Снимаем то, что могли поставить сами или что уже есть в шаблоне,
    // чтобы результат не зависел от того, сколько раз сюда зашли
    size_t start = 0;
    if (input[start] == '\x01') ++start;
    if (start < input.size() && input[start] == ' ') ++start;

    return std::string("\x01 ") + input.substr(start);
}

bool IsKnownColorTag(const std::string& tag) {
    for (const char* known : kKnownTags) {
        if (tag.size() == std::strlen(known) && EqualsIgnoreCaseAt(tag, 0, known)) return true;
    }
    return false;
}

const char* const* KnownColorTagList(int* count) {
    *count = static_cast<int>(sizeof(kKnownTags) / sizeof(kKnownTags[0]));
    return kKnownTags;
}

}  // namespace text
}  // namespace nm
