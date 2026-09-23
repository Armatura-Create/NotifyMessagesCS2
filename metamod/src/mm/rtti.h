// Поиск vtable класса серверного модуля по его RTTI-имени.
//
// Нужен ровно для одного класса — CGameEventManager: менеджер игровых событий
// фабрика не отдаёт, а без него нет ни HTML-панели, ни подавления штатных
// сообщений «вышел из игры» / «перешёл в команду», ни анонса смены команды.
//
// Это не сигнатура и не смещение: имя класса игра не меняет от обновления к
// обновлению, а RTTI компилятор кладёт всегда. Алгоритм — из CS2Fixes
// (src/utils/plat_*.cpp, GPL-3.0): Itanium ABI на Linux, MSVC RTTI на Windows.
//
// Только главный поток, только после загрузки server-модуля (то есть из Load()).
#pragma once

namespace nm {
namespace rtti {

// Адрес vtable (первый виртуальный метод) или nullptr. Причина отказа — в *error.
void* FindServerVTable(const char* className, const char** error);

}  // namespace rtti
}  // namespace nm
