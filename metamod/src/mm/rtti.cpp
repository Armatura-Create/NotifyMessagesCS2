#include "mm/rtti.h"

#include "core/rtti_search.h"

#include <cstdint>
#include <cstring>
#include <map>
#include <string>
#include <vector>

#ifdef _WIN32
#include <windows.h>
#else
#include <elf.h>
#include <fcntl.h>
#include <link.h>
#include <unistd.h>
#endif

namespace nm {
namespace rtti {
namespace {

using Sections = std::map<std::string, MemoryRange>;

#ifdef _WIN32

// Секции PE лежат в памяти модуля — читать заголовки можно прямо оттуда
bool LoadServerSections(Sections* out, uintptr_t* base, const char** error) {
    HMODULE module = GetModuleHandleA("server.dll");
    if (module == nullptr) {
        *error = "server.dll не загружен";
        return false;
    }

    const uint8_t* image = reinterpret_cast<const uint8_t*>(module);
    const auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(image);
    const auto* nt = reinterpret_cast<const IMAGE_NT_HEADERS64*>(image + dos->e_lfanew);
    const IMAGE_SECTION_HEADER* section = IMAGE_FIRST_SECTION(nt);

    for (WORD i = 0; i < nt->FileHeader.NumberOfSections; ++i) {
        const char* name = reinterpret_cast<const char*>(section[i].Name);
        const std::string key(name, strnlen(name, IMAGE_SIZEOF_SHORT_NAME));
        (*out)[key] = MemoryRange{image + section[i].VirtualAddress, section[i].Misc.VirtualSize};
    }

    *base = reinterpret_cast<uintptr_t>(image);
    return true;
}

#else

struct Found {
    std::string path;
    uintptr_t base = 0;
};

int FindServerModule(dl_phdr_info* info, size_t, void* context) {
    const char* name = info->dlpi_name;
    const size_t length = name != nullptr ? std::strlen(name) : 0;
    const char* suffix = "/libserver.so";
    const size_t suffixLength = std::strlen(suffix);
    if (length < suffixLength || std::strcmp(name + length - suffixLength, suffix) != 0) return 0;

    Found* found = static_cast<Found*>(context);
    found->path = name;
    found->base = static_cast<uintptr_t>(info->dlpi_addr);
    return 1;
}

// Чтение ровно size байт с позиции offset. pread, а не mmap файла: страничный
// отказ в отображённом файле внутри игрового процесса — это SIGBUS
// (ровно так уже падал сервер на базах GeoIP).
bool ReadAt(int fd, uint64_t offset, void* buffer, size_t size) {
    uint8_t* at = static_cast<uint8_t*>(buffer);
    while (size > 0) {
        const ssize_t got = pread(fd, at, size, static_cast<off_t>(offset));
        if (got <= 0) return false;
        at += got;
        offset += static_cast<uint64_t>(got);
        size -= static_cast<size_t>(got);
    }
    return true;
}

// Заголовки секций в память модуля не грузятся — их читаем из файла, а адреса
// секций пересчитываем от базы загрузки
bool LoadServerSections(Sections* out, uintptr_t* base, const char** error) {
    Found found;
    dl_iterate_phdr(FindServerModule, &found);
    if (found.path.empty()) {
        *error = "libserver.so не загружен";
        return false;
    }

    const int fd = open(found.path.c_str(), O_RDONLY | O_CLOEXEC);
    if (fd < 0) {
        *error = "libserver.so не открывается на чтение";
        return false;
    }

    bool ok = false;
    Elf64_Ehdr header;
    if (ReadAt(fd, 0, &header, sizeof(header)) && std::memcmp(header.e_ident, ELFMAG, SELFMAG) == 0 &&
        header.e_shentsize == sizeof(Elf64_Shdr) && header.e_shstrndx < header.e_shnum) {
        std::vector<Elf64_Shdr> sections(header.e_shnum);
        if (ReadAt(fd, header.e_shoff, sections.data(), sections.size() * sizeof(Elf64_Shdr))) {
            const Elf64_Shdr& names = sections[header.e_shstrndx];
            std::vector<char> strings(names.sh_size + 1, '\0');
            if (ReadAt(fd, names.sh_offset, strings.data(), names.sh_size)) {
                for (const Elf64_Shdr& section : sections) {
                    // Только секции, которые загружены в память процесса
                    if ((section.sh_flags & SHF_ALLOC) == 0 || section.sh_name >= names.sh_size) continue;
                    (*out)[strings.data() + section.sh_name] = MemoryRange{
                        reinterpret_cast<const uint8_t*>(found.base + section.sh_addr),
                        static_cast<size_t>(section.sh_size)};
                }
                ok = true;
            }
        }
    }
    close(fd);

    if (!ok) *error = "не удалось прочитать заголовки секций libserver.so";
    *base = found.base;
    return ok;
}

#endif

}  // namespace

void* FindServerVTable(const char* className, const char** error) {
    Sections sections;
    uintptr_t base = 0;
    if (!LoadServerSections(&sections, &base, error)) return nullptr;

    const void* vtable = nullptr;
#ifdef _WIN32
    const auto data = sections.find(".data");
    const auto rdata = sections.find(".rdata");
    if (data == sections.end() || rdata == sections.end()) {
        *error = "в server.dll нет секций .data/.rdata";
        return nullptr;
    }
    vtable = FindMsvcVTable(className, base, data->second, rdata->second);
#else
    const auto rodata = sections.find(".rodata");
    if (rodata == sections.end()) {
        *error = "в libserver.so нет секции .rodata";
        return nullptr;
    }
    std::vector<MemoryRange> relro;
    for (const char* name : {".data.rel.ro", ".data.rel.ro.local"}) {
        const auto section = sections.find(name);
        if (section != sections.end()) relro.push_back(section->second);
    }
    vtable = FindItaniumVTable(className, rodata->second, relro);
#endif

    if (vtable == nullptr) *error = "RTTI класса не найден";
    return const_cast<void*>(vtable);
}

}  // namespace rtti
}  // namespace nm
