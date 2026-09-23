# NotifyMessages — цель Metamod:Source

Нативный плагин на C++. **Не** обёртка над версиями для CounterStrikeSharp или SwiftlyS2:
Metamod не хостит .NET, поэтому это отдельная реализация. Общий с остальными целями — только
формат конфигов: `Settings.json`, `Messages.json`, `Ads.json` и `Servers.json` переносятся между
тремя целями без правок, а файлы первого запуска совпадают с C#-версией байт в байт.

Минимум — **Metamod:Source 2.0 git1460**: 8 сентября 2026 Metamod убрал SourceHook и заменил
его на KHook, подняв версию API плагинов. Плагин собран под новый API и на более старых
сборках не загрузится.

## Как устроено

```
addons/
├── metamod/NotifyMessages.vdf
├── configs/NotifyMessages/        ← создаётся при первом запуске
└── NotifyMessages/
    ├── bin/linuxsteamrt64/NotifyMessages.so   (Windows: bin/win64/NotifyMessages.dll)
    ├── GeoLite2-Country.mmdb
    └── GeoLite2-City.mmdb
```

**Ни сигнатур, ни смещений.** Всё, что плагин делает, идёт через то, что игра не меняет от
обновления к обновлению:

| Что | Откуда |
|---|---|
| игроки: SteamID, ник, IP | хуки `IServerGameClients` (`OnClientConnected`, `ClientPutInServer`, `ClientFullyConnect`, `ClientDisconnect`) |
| язык игрока | userinfo-квар `cl_language` через `IVEngineServer2::GetClientConVarValue` |
| чат, центр, alert | UserMessage `TextMsg` (`INetworkMessages` + `IGameEventSystem`) — то же сообщение, что собирает `ClientPrint` игры |
| консоль игрока | `IVEngineServer2::ClientPrintf` |
| HTML-панель | событие `show_survival_respawn_status`, сериализованное и отправленное одному игроку |
| смена команды, скрытие штатных сообщений | хук `IGameEventManager2::FireEvent` |
| `!servers` в чате | хук `ICvar::DispatchConCommand` (`say` / `say_team`) |
| таймеры | свой планировщик в `GameFrame` |

Менеджер игровых событий фабрика движка не отдаёт. Его vtable ищется по RTTI-имени класса
`CGameEventManager` (`src/mm/rtti.cpp` + `src/core/rtti_search.cpp`, алгоритм из CS2Fixes), а
сам объект приходит первым аргументом перехваченного `FireEvent`. Если класс не найден, плагин
говорит об этом при загрузке и продолжает работать: `CenterHtml` выводится обычным центром,
смена команды не анонсируется, остальное — как обычно.

Сознательные отличия от C#-целей:

| Что | Почему |
|---|---|
| админские `mm_*` — только консоль сервера или rcon | своей системы прав у Metamod нет |
| `Settings.ShowHtmlWhenDead` не действует (как в SwiftlyS2) | пауза панели на время смерти требует читать пешку, то есть смещения |
| `{PLAYERS}` — все в игре, включая ботов | в C# это «пешка валидна», здесь пешки не читаются |

## Что проверяется локально

Ядро (`src/core`) не знает ни про hl2sdk, ни про Metamod:

```bash
git submodule update --init --recursive
make -f Makefile.tests -j8 && ./build-tests/nm_tests   # поведение
make -f Makefile.tests noexcept-check                  # совместимость с игровой сборкой
make -f Makefile.tests sources-check                   # AMBuilder собирает все файлы ядра
```

Около сотни тестов за секунду на любой машине: конфиги (включая битые и типы полей), рендер во
всех каналах, локализация, диагностика шаблонов, реклама, планировщик, HTML-центр по слотам,
разбор A2S и живой опрос через петлю (challenge и split-пакеты), открытие настоящих баз
GeoLite2 из памяти, поиск vtable на синтетических таблицах RTTI.

**Зелёная локальная сборка не равна зелёной сборке в CI.** На macOS это Apple clang с libc++,
в CI — clang/g++ с libstdc++: заголовок, забывший `#include <cstdint>`, соберётся локально и
упадёт в CI. Игровая сборка идёт с `-fno-exceptions` и `-std=c++20` — в этом режиме
nlohmann/json на ошибке зовёт `std::abort()`, поэтому `noexcept-check` обязателен.

## Сборка самого плагина

Слой движка (`src/mm`) требует hl2sdk, Metamod:Source и protobuf; на macOS не собирается.
В CI это делает `.github/workflows/ci-metamod.yml`: контейнер Steam Runtime 3 для Linux
(это ABI выделенного сервера — бинарник с обычного ubuntu не загрузится) и `windows-latest`.

Локально под Linux:

```bash
git clone --recurse-submodules https://github.com/alliedmodders/hl2sdk -b cs2 sdk
git clone https://github.com/alliedmodders/hl2sdk-manifests
git clone --recurse-submodules https://github.com/alliedmodders/metamod-source ../../mmsource-2.0
mkdir build && cd build
HL2SDKCS2=../sdk python ../configure.py --enable-optimize --sdks cs2 && ambuild
```

На любой машине с Docker — в том же контейнере, что и CI (на arm64 через эмуляцию, медленно):

```bash
docker run --rm --platform linux/amd64 -v "$PWD/..":/w/NotifyMessages -v <sdk>:/w/sdk \
  -v <hl2sdk-manifests>:/w/hl2sdk-manifests -v <metamod-source>:/w/mms \
  ghcr.io/source2ze/build-containers:steamrt3 bash -c 'cd /w/NotifyMessages/metamod && \
  mkdir -p build-docker && cd build-docker && HL2SDKCS2=/w/sdk python3 ../configure.py \
  --enable-optimize --sdks cs2 --hl2sdk-manifests /w/hl2sdk-manifests --mms_path /w/mms && ambuild'
```

## Вендоры

| Библиотека | Зачем | Лицензия |
|---|---|---|
| `libmaxminddb` | GeoLite2 (открывается из памяти обёрткой `src/core/geoip_maxminddb.c`) | Apache-2.0 |
| `nlohmann/json` | конфиги | MIT |
| `doctest` | тесты | MIT |

`AMBuildScript`, `configure.py` и алгоритм поиска vtable по RTTI взяты из
[CS2Fixes](https://github.com/Source2ZE/CS2Fixes) (GPL-3.0) через соседний плагин ConnectHistory
и адаптированы: своя версия означала бы повторять по памяти три сотни строк определения hl2sdk
и разъезжаться с ними при каждом обновлении SDK.
