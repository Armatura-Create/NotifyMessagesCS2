// Логгер ядра. Ядро не знает ни про Metamod, ни про движок: наружу выставлен
// интерфейс, который в игре пишет в консоль сервера, а в тестах — в память.
//
// Интерфейс с «одной реализацией» оправдан ровно здесь: без него ни один
// инвариант ядра нельзя было бы проверить без запущенного CS2.
#pragma once

#include <string>

namespace nm {

class ILogger {
public:
    virtual ~ILogger() = default;

    virtual void Info(const std::string& message) = 0;
    virtual void Error(const std::string& message) = 0;

    // Debug пишет SteamID, ники и гео игроков — печатается только при Settings.Debug.
    virtual void Debug(const std::string& message) = 0;
};

class NullLogger final : public ILogger {
public:
    void Info(const std::string&) override {}
    void Error(const std::string&) override {}
    void Debug(const std::string&) override {}
};

}  // namespace nm
