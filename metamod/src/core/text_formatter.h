// Цветовые теги и грамматики каналов вывода.
//
// Порт Utils/TextFormatter.cs. Коды цветов — управляющие байты самого движка CS2,
// выписанные из ChatColors CounterStrikeSharp 1.0.369 (swiftly/ хранит ту же
// таблицу). При расхождении верна cssharp-версия: там коды берутся из фреймворка.
#pragma once

#include <string>

namespace nm {
namespace text {

// Теги -> управляющие байты чата. {SPACE} -> широкий пробел, размеры
// ({BIG} {MEDIUM} {SMALL}) в чате вырезаются молча.
std::string ReplaceColorTags(const std::string& input);

// Рендер для HTML-центра: цвет тегом <font>, перенос строки — <br>. Все
// открытые <font> закрываются в конце, чтобы следующее сообщение не унаследовало
// цвет и размер предыдущего.
std::string ToCenterHtml(const std::string& input);

// Убирает цветовые теги без подстановки — для каналов, где цвета не рисуются.
std::string RemoveColorTags(const std::string& input);

// Ник игрока — недоверенные данные: без этого «<img src=x>» ломает HTML-панель
// всем зрителям.
std::string EscapeHtml(const std::string& input);

// Байты 0x01..0x10 наружу — для логов.
std::string StripColorCodes(const std::string& input);

// Приводит строку для чата к виду "\x01 " + текст.
//
// Движок не применяет цвет, стоящий в самом начале сообщения. Ранний выход
// «уже начинается с кода цвета» — ровно тот баг, из-за которого {LIGHTBLUE}Server
// выходил белым. Функция идемпотентна.
std::string EnsureChatColorPrefix(const std::string& input);

// Замена всех вхождений без учёта регистра ASCII. UTF-8 безопасен: байты
// многобайтовых символов никогда не совпадают с ASCII.
std::string ReplaceIgnoreCase(const std::string& text, const std::string& search,
                              const std::string& replacement);

// Замена всех вхождений с учётом регистра (string.Replace в C#).
std::string ReplaceOrdinal(const std::string& text, const std::string& search,
                           const std::string& replacement);

bool ContainsIgnoreCase(const std::string& text, const std::string& search);

// Тег, который подставит один из рендеров: цвета, {SPACE}, размеры.
// Диагностика шаблонов берёт список отсюда, а не заводит свой.
bool IsKnownColorTag(const std::string& tag);

// Все известные теги — для тестов, проверяющих каждый канал.
const char* const* KnownColorTagList(int* count);

}  // namespace text
}  // namespace nm
