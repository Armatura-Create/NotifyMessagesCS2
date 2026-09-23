// Таймеры и «следующий кадр» для нативной цели.
//
// В C#-целях это AddTimer / Server.NextFrame (CSSharp) и Core.Scheduler (SwiftlyS2).
// У Metamod своего планировщика нет, поэтому он здесь: плагин зовёт RunFrame из
// хука GameFrame, и все задачи выполняются в главном потоке.
//
// Время — монотонные часы, переданные снаружи: тесты подставляют свои.
#pragma once

#include <cstdint>
#include <functional>
#include <map>
#include <mutex>
#include <utility>
#include <vector>

namespace nm {

class Scheduler {
public:
    using Task = std::function<void()>;
    using Clock = std::function<double()>;  // секунды
    using Id = uint64_t;

    explicit Scheduler(Clock clock) : _clock(std::move(clock)) {}

    Id Delay(double seconds, Task task);
    // Первый запуск — через interval, а не сразу: реклама не должна
    // выстреливать в момент загрузки плагина (как DelayAndRepeat в SwiftlyS2)
    Id Repeat(double interval, Task task);
    void Cancel(Id id);

    // Единственный метод, который можно звать из фонового потока: так опрос A2S
    // возвращает логи и результаты в главный поток.
    void NextFrame(Task task);

    // Главный поток, каждый кадр
    void RunFrame();

    void Clear();

    double Now() const { return _clock(); }

private:
    struct Timer {
        double due = 0;
        double interval = 0;  // 0 — одноразовый
        Task task;
    };

    Id Add(double delay, double interval, Task task);

    Clock _clock;
    Id _nextId = 1;
    std::map<Id, Timer> _timers;

    std::mutex _postedMutex;
    std::vector<Task> _posted;
};

}  // namespace nm
