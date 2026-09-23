#include "core/rtti_search.h"

#include <cstring>

namespace nm {
namespace rtti {
namespace {

// Чтение без требований к выравниванию: memcpy компилятор сводит к одной загрузке
template <typename T>
T Load(const uint8_t* at) {
    T value;
    std::memcpy(&value, at, sizeof(T));
    return value;
}

// Все вхождения байтов needle в range
std::vector<const uint8_t*> FindAll(MemoryRange range, const std::string& needle) {
    std::vector<const uint8_t*> found;
    if (needle.empty() || range.size < needle.size()) return found;

    const uint8_t first = static_cast<uint8_t>(needle[0]);
    const uint8_t* end = range.base + range.size - needle.size() + 1;
    for (const uint8_t* at = range.base; at < end; ++at) {
        at = static_cast<const uint8_t*>(std::memchr(at, first, static_cast<size_t>(end - at)));
        if (at == nullptr) break;
        if (std::memcmp(at, needle.data(), needle.size()) == 0) found.push_back(at);
    }
    return found;
}

// Выровненные слоты range, где лежит значение value
template <typename T>
std::vector<const uint8_t*> FindValue(MemoryRange range, T value) {
    std::vector<const uint8_t*> found;
    const uintptr_t start = reinterpret_cast<uintptr_t>(range.base);
    const uintptr_t aligned = (start + sizeof(T) - 1) & ~(static_cast<uintptr_t>(sizeof(T)) - 1);
    for (uintptr_t at = aligned; at + sizeof(T) <= start + range.size; at += sizeof(T)) {
        const uint8_t* slot = reinterpret_cast<const uint8_t*>(at);
        if (Load<T>(slot) == value) found.push_back(slot);
    }
    return found;
}

bool Contains(MemoryRange range, const uint8_t* at, size_t size) {
    return at >= range.base && at + size <= range.base + range.size;
}

}  // namespace

const void* FindItaniumVTable(const std::string& className, MemoryRange rodata,
                              const std::vector<MemoryRange>& relro) {
    // Имя типа класса в глобальном пространстве имён: длина + имя, с нулём в конце
    std::string mangled = std::to_string(className.size()) + className;
    mangled.push_back('\0');

    for (const uint8_t* name : FindAll(rodata, mangled)) {
        // Строка обязана начинаться здесь, а не быть хвостом "P17…" или "N3foo17…"
        if (name != rodata.base && name[-1] != '\0') continue;

        for (const MemoryRange& refs : relro) {
            for (const uint8_t* nameField : FindValue<uintptr_t>(refs, reinterpret_cast<uintptr_t>(name))) {
                // typeinfo: [vptr][name]… — поле имени идёт вторым
                if (!Contains(refs, nameField - sizeof(uintptr_t), sizeof(uintptr_t))) continue;
                const uintptr_t typeinfo = reinterpret_cast<uintptr_t>(nameField) - sizeof(uintptr_t);

                for (const MemoryRange& tables : relro) {
                    for (const uint8_t* slot : FindValue<uintptr_t>(tables, typeinfo)) {
                        // vtable: [offset-to-top = 0][typeinfo][методы…]. Ссылки на typeinfo
                        // из typeinfo наследников и вторичные vtable (offset ≠ 0) отсеиваются
                        if (!Contains(tables, slot - sizeof(intptr_t), sizeof(intptr_t))) continue;
                        if (Load<intptr_t>(slot - sizeof(intptr_t)) != 0) continue;
                        if (!Contains(tables, slot + sizeof(uintptr_t), sizeof(uintptr_t))) continue;
                        return slot + sizeof(uintptr_t);
                    }
                }
            }
        }
    }
    return nullptr;
}

const void* FindMsvcVTable(const std::string& className, uintptr_t moduleBase, MemoryRange data,
                           MemoryRange rdata) {
    for (const char* prefix : {".?AV", ".?AU"}) {  // class и struct
        std::string decorated = prefix + className + "@@";
        decorated.push_back('\0');

        for (const uint8_t* name : FindAll(data, decorated)) {
            // TypeDescriptor: [pVFTable][spare][name…] — имя с шестнадцатого байта
            const uintptr_t descriptor = reinterpret_cast<uintptr_t>(name) - 0x10;
            if (descriptor < moduleBase) continue;
            const uint32_t rva = static_cast<uint32_t>(descriptor - moduleBase);

            for (const uint8_t* field : FindValue<uint32_t>(rdata, rva)) {
                // Complete Object Locator: [signature=1][offset=0][cdOffset][pTypeDescriptor]…
                const uint8_t* locator = field - 12;
                if (!Contains(rdata, locator, 12)) continue;
                if (Load<int32_t>(locator) != 1 || Load<int32_t>(locator + 4) != 0) continue;

                // vtable[-1] указывает на COL
                for (const uint8_t* slot : FindValue<uintptr_t>(rdata, reinterpret_cast<uintptr_t>(locator))) {
                    if (!Contains(rdata, slot + sizeof(uintptr_t), sizeof(uintptr_t))) continue;
                    return slot + sizeof(uintptr_t);
                }
            }
        }
    }
    return nullptr;
}

}  // namespace rtti
}  // namespace nm
