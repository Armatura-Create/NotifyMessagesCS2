// Поиск vtable по RTTI на синтетических таблицах. На живом сервере то же самое
// ищется в libserver.so / server.dll; раскладка RTTI при этом та же.
#include "core/rtti_search.h"

#include "doctest.h"

#include <cstring>

using namespace nm::rtti;

namespace {

void Put(uint8_t* at, uintptr_t value) { std::memcpy(at, &value, sizeof(value)); }
void Put32(uint8_t* at, uint32_t value) { std::memcpy(at, &value, sizeof(value)); }

}  // namespace

TEST_CASE("Itanium: имя -> typeinfo -> vtable с нулевым offset-to-top") {
    alignas(8) uint8_t rodata[128] = {};
    alignas(8) uint8_t relro[256] = {};

    // Приманка: typeinfo указателя на класс — хвост имени совпадает, начало нет
    std::memcpy(rodata + 4, "P17CGameEventManager", 21);
    const char* real = "17CGameEventManager";
    std::memcpy(rodata + 40, real, std::strlen(real) + 1);

    // typeinfo приманки и настоящий: [vptr][name]
    Put(relro + 0, 0x1111);
    Put(relro + 8, reinterpret_cast<uintptr_t>(rodata + 5));
    Put(relro + 16, 0x2222);
    Put(relro + 24, reinterpret_cast<uintptr_t>(rodata + 40));
    const uintptr_t typeinfo = reinterpret_cast<uintptr_t>(relro + 16);

    // typeinfo наследника ссылается на наш как на базу — перед ссылкой не ноль
    Put(relro + 32, 0x3333);
    Put(relro + 40, 0x4444);
    Put(relro + 48, typeinfo);

    // Вторичная vtable: offset-to-top = -8
    Put(relro + 64, static_cast<uintptr_t>(-8));
    Put(relro + 72, typeinfo);
    Put(relro + 80, 0x5555);

    // Настоящая vtable: [0][typeinfo][методы]
    Put(relro + 96, 0);
    Put(relro + 104, typeinfo);
    Put(relro + 112, 0x6666);

    const void* vtable = FindItaniumVTable("CGameEventManager", {rodata, sizeof(rodata)}, {{relro, sizeof(relro)}});
    CHECK(vtable == relro + 112);
}

TEST_CASE("Itanium: класса нет — nullptr") {
    alignas(8) uint8_t rodata[32] = {};
    alignas(8) uint8_t relro[32] = {};
    std::memcpy(rodata, "8COtherXX", 10);
    CHECK(FindItaniumVTable("CGameEventManager", {rodata, sizeof(rodata)}, {{relro, sizeof(relro)}}) == nullptr);
}

TEST_CASE("MSVC: TypeDescriptor -> Complete Object Locator -> vtable") {
    alignas(16) uint8_t module[512] = {};
    const uintptr_t base = reinterpret_cast<uintptr_t>(module);
    uint8_t* data = module + 64;    // .data
    uint8_t* rdata = module + 256;  // .rdata

    // TypeDescriptor: [pVFTable][spare][".?AVCGameEventManager@@"]
    const char* name = ".?AVCGameEventManager@@";
    std::memcpy(data + 16, name, std::strlen(name) + 1);
    const uint32_t descriptorRva = static_cast<uint32_t>(reinterpret_cast<uintptr_t>(data) - base);

    // Ложный COL: подпись не 1
    Put32(rdata + 0, 0);
    Put32(rdata + 4, 0);
    Put32(rdata + 12, descriptorRva);

    // Настоящий COL: [1][offset 0][cd][pTypeDescriptor]
    Put32(rdata + 32, 1);
    Put32(rdata + 36, 0);
    Put32(rdata + 40, 0);
    Put32(rdata + 44, descriptorRva);

    // vtable[-1] -> COL
    Put(rdata + 96, reinterpret_cast<uintptr_t>(rdata + 32));
    Put(rdata + 104, 0x7777);

    const void* vtable = FindMsvcVTable("CGameEventManager", base, {data, 128}, {rdata, 256});
    CHECK(vtable == rdata + 104);
}
