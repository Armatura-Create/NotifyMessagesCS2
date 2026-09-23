/* Открытие базы MaxMind ИЗ ПАМЯТИ вместо mmap.
 *
 * mmap внутри игрового процесса превращает любой сбой чтения страницы в SIGBUS
 * и убивает сервер без единой строки в логе. Реальный инцидент, ради которого
 * это написано, произошёл при полностью целом файле — на overlayfs в Docker.
 */
#ifndef NM_GEOIP_MEMORY_H
#define NM_GEOIP_MEMORY_H

/* maxminddb.h declares `unsigned __int128`, which ISO C++ does not have, so a
 * -Wpedantic build (ours is -Werror) refuses the header outright. The type is
 * correct and wanted - the database format really does carry 128-bit values -
 * so the warning is silenced for this include only, not project-wide. */
#if defined(__GNUC__) || defined(__clang__)
#pragma GCC diagnostic push
#pragma GCC diagnostic ignored "-Wpedantic"
#endif

#include "maxminddb.h"

#if defined(__GNUC__) || defined(__clang__)
#pragma GCC diagnostic pop
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* MMDB_SUCCESS или код ошибки libmaxminddb. */
int nm_mmdb_open_memory(const char *const filename, MMDB_s *const mmdb);

/* Освобождает то, что открыл nm_mmdb_open_memory. MMDB_close сюда не подходит:
   он попытается сделать munmap на обычном буфере. */
void nm_mmdb_close_memory(MMDB_s *const mmdb);

#ifdef __cplusplus
}
#endif

#endif /* NM_GEOIP_MEMORY_H */
