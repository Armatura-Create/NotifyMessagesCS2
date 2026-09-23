#include "core/scheduler.h"

#include <utility>

namespace nm {

Scheduler::Id Scheduler::Add(double delay, double interval, Task task) {
    const Id id = _nextId++;
    Timer timer;
    timer.due = _clock() + (delay > 0 ? delay : 0);
    timer.interval = interval;
    timer.task = std::move(task);
    _timers.emplace(id, std::move(timer));
    return id;
}

Scheduler::Id Scheduler::Delay(double seconds, Task task) { return Add(seconds, 0, std::move(task)); }

Scheduler::Id Scheduler::Repeat(double interval, Task task) {
    // Нулевой интервал выполнял бы задачу каждый кадр — это не таймер, а ошибка
    const double safe = interval > 0.01 ? interval : 0.01;
    return Add(safe, safe, std::move(task));
}

void Scheduler::Cancel(Id id) { _timers.erase(id); }

void Scheduler::NextFrame(Task task) {
    std::lock_guard<std::mutex> guard(_postedMutex);
    _posted.push_back(std::move(task));
}

void Scheduler::RunFrame() {
    std::vector<Task> posted;
    {
        std::lock_guard<std::mutex> guard(_postedMutex);
        posted.swap(_posted);
    }
    for (Task& task : posted) {
        if (task) task();
    }

    const double now = _clock();

    // Сначала список созревших, потом запуск: задача может отменить или завести
    // другие таймеры, и обход живого словаря это сломало бы
    std::vector<Id> due;
    for (const auto& timer : _timers) {
        if (timer.second.due <= now) due.push_back(timer.first);
    }

    for (const Id id : due) {
        const auto timer = _timers.find(id);
        if (timer == _timers.end()) continue;  // отменили предыдущие задачи этого кадра

        Task task = timer->second.task;
        if (timer->second.interval > 0) {
            timer->second.due = now + timer->second.interval;
        } else {
            _timers.erase(timer);
        }
        if (task) task();
    }
}

void Scheduler::Clear() {
    _timers.clear();
    std::lock_guard<std::mutex> guard(_postedMutex);
    _posted.clear();
}

}  // namespace nm
