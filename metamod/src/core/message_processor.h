// Локализация, контекстные значения, системные теги и рендер под канал.
//
// Порт Services/MessageProcessor.cs. Движка здесь нет ни строкой: всё, что о нём
// нужно знать, приходит через IServerInfoSource. Поэтому класс проверяется тестами
// на любой машине.
//
// ВЫЗЫВАТЬ ТОЛЬКО ИЗ ГЛАВНОГО ПОТОКА: за IServerInfoSource стоят ConVar и globals.
#pragma once

#include "core/config.h"

#include <cstdint>
#include <functional>
#include <string>
#include <utility>
#include <vector>

namespace nm {

// Факты о сервере для {MAP} {SERVERNAME} {IP} {PORT} {MAXPLAYERS} {PLAYERS}.
// Реализует плагин — только он умеет читать движок. Свойства читаются лениво:
// в Load() и конструкторах их не трогать.
class IServerInfoSource {
public:
    virtual ~IServerInfoSource() = default;

    virtual std::string MapName() = 0;
    virtual std::string Hostname() = 0;
    virtual std::string Ip() = 0;
    virtual std::string Port() = 0;
    virtual int MaxPlayers() = 0;
    virtual int Players() = 0;
};

// Контекстные значения ({PLAYERNAME}, {TEAM}, {SECONDS}, …) в порядке подстановки
using Values = std::vector<std::pair<std::string, std::string>>;

// SteamID -> языковой блок из Messages.json. Пусто — язык неизвестен.
using LanguageResolver = std::function<std::string(uint64_t)>;

// Одно совпадение \{([^}]*)\}: весь тег и имя внутри скобок
struct TagMatch {
    std::string tag;
    std::string name;
};

class MessageProcessor {
public:
    MessageProcessor(const Config* config, LanguageResolver language, IServerInfoSource* server)
        : _config(config), _language(std::move(language)), _server(server) {}

    std::string LanguageOf(uint64_t steamId) const { return _language(steamId); }

    // Порядок частей не произволен: язык -> значения -> системные теги -> рендер.
    // Значения подставляются ДО рендера, потому что сами содержат теги
    // ({TEAM} = "{RED}Terrorists{DEFAULT}"). Пока подстановка шла после
    // ProcessMessage, игроки видели эти теги текстом.
    std::string ProcessMessage(const std::string& message, uint64_t steamId,
                               MessageType channel = MessageType::Chat,
                               const Values* values = nullptr) const;

    // Случайный ШАБЛОН из набора Join/Leave на языке получателя. Значения
    // подставляет ProcessMessage — единственная точка подстановки.
    std::string GetRandomLocalizedMessage(const CiMap<std::vector<std::string>>& messages,
                                          uint64_t recipientSteamId) const;

    // Системные теги и имена карт. Каждый тег резолвится, только если реально есть
    // в строке: иначе на каждое сообщение уходили бы ConVar и globals.
    std::string ReplaceMessageTags(const std::string& message) const;

    // Для HTML-канала значения экранируются: ник — недоверенные данные.
    static std::string ApplyValues(const std::string& message, const Values* values,
                                   MessageType channel);

    // Единственное место, где строка становится специфичной для канала.
    static std::string Render(const std::string& text, MessageType channel);

    static bool IsSystemTag(const std::string& tag);

    // Все {…} по порядку — как Regex.Matches(@"\{([^}]*)\}")
    static std::vector<TagMatch> FindTags(const std::string& text);

private:
    std::string ApplyLanguage(const std::string& message, uint64_t steamId) const;

    const Config* _config;
    LanguageResolver _language;
    IServerInfoSource* _server;
};

}  // namespace nm
