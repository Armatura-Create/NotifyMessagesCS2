// Файловая система: ровно то, чего не хватает загрузке конфига.
#pragma once

#include <string>

namespace nm {
namespace fs {

bool DirectoryExists(const std::string& path);

// Создаёт каталог ВМЕСТЕ С РОДИТЕЛЯМИ, как Directory.CreateDirectory в C#.
// Одиночный mkdir полного пути падает с ENOENT, если промежуточного каталога нет,
// а на свежем сервере нет и addons/configs.
bool EnsureDirectory(const std::string& path);

}  // namespace fs
}  // namespace nm
