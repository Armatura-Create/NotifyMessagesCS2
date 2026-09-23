// Поиск vtable класса по RTTI в уже загруженных секциях модуля.
//
// Чистая арифметика над диапазонами памяти: какие секции у модуля и где они
// лежат, выясняет mm/rtti.cpp (ELF / PE), а здесь — только раскладка RTTI.
// Поэтому алгоритм проверяется тестами на синтетических таблицах — иначе его
// проверил бы только живой сервер.
//
// Алгоритм — из CS2Fixes (src/utils/plat_unix.cpp, plat_win.cpp; GPL-3.0),
// с одним ужесточением: имя типа должно начинаться с начала строки, иначе
// "P17CGameEventManager" (typeinfo указателя) сошёл бы за сам класс.
#pragma once

#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

namespace nm {
namespace rtti {

struct MemoryRange {
    const uint8_t* base = nullptr;
    size_t size = 0;
};

// Itanium C++ ABI (Linux). rodata — где лежит имя типа ("17CGameEventManager");
// relro — .data.rel.ro и .data.rel.ro.local, где typeinfo и сама vtable.
const void* FindItaniumVTable(const std::string& className, MemoryRange rodata,
                              const std::vector<MemoryRange>& relro);

// MSVC x64 (Windows). data — где TypeDescriptor (".?AVCGameEventManager@@"),
// rdata — где Complete Object Locator и vtable; moduleBase — для RVA.
const void* FindMsvcVTable(const std::string& className, uintptr_t moduleBase, MemoryRange data,
                           MemoryRange rdata);

}  // namespace rtti
}  // namespace nm
