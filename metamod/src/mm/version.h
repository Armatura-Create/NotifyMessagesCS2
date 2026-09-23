// Версия плагина. В релизе файл перезаписывается из тега перед сборкой
// (см. .github/workflows/release.yml): у версии ровно один источник — тег.
// Значение ниже — фолбэк для локальных сборок.
#pragma once

#ifndef NM_VERSION
#define NM_VERSION "0.0.0-dev"
#endif
