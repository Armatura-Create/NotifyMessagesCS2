# CLAUDE.md

Плагин **NotifyMessages** для CounterStrikeSharp (CS2): уведомления, реклама, приветствия,
локализация по GeoIP и мониторинг чужих серверов через A2S.

- `net10.0`, namespace `NotifyMessages`, `[MinimumApiVersion(369)]`
- Зависимости: `CounterStrikeSharp.API` `1.0.369` (пин, не `*`), `MaxMind.GeoIP2` `5.3.0`
- **Собираемся против МИНИМАЛЬНОЙ поддерживаемой версии CSSharp, а не самой свежей.** Тогда
  компиляция сама доказывает, что API из более новых сборок не используется, и плагин грузится
  на любом сервере с 1.0.369+. Версия пакета в `.csproj` и `[MinimumApiVersion]` обязаны
  совпадать — это проверяет `ApiVersionTests`. Реальный инцидент: с `MinimumApiVersion(373)`
  сервер на 1.0.371 отказался грузить плагин, хотя ничего из 372-373 в коде не было.
- `net10.0` — не выбор, а требование: CSSharp с v1.0.369 работает на .NET 10, и пакет
  `CounterStrikeSharp.API` таргетит `net10.0`. Официальные docs всё ещё показывают `net8.0` — они отстали.
- SDK стоит в `~/.dotnet` (не в PATH по умолчанию): `export PATH="$HOME/.dotnet:$PATH"`

## Команды

```bash
dotnet build                 # обычная сборка
dotnet build -c Release      # + упаковка в bin/Release/net10.0/NotifyMessages.zip
dotnet test                  # xUnit-тесты чистой логики
```

Тесты лежат в `tests/NotifyMessages.Tests/` и покрывают то, что уже ломалось: разбор
недоверенных A2S-пакетов, цветовые теги, `GeoIpService.ExtractIp`, ротацию рекламы,
`RestartNotifyConfig.ResolveTemplate`. Внутренности открыты тестам через
`Properties/AssemblyInfo.cs` (`InternalsVisibleTo`) — публичный API ради тестов не расширяем.

Каталог `tests/**` исключён из компиляции плагина в `.csproj` — он лежит внутри дерева
проекта, и без `<Compile Remove>` SDK-глоб затянул бы его в саму сборку плагина.

CI: `.github/workflows/ci.yml` (push/PR) и `release.yml` (тег `v*` → сборка, тесты, zip,
GitHub Release). Релиз не публикуется на красных тестах.

Release-сборка (таргет `PackageRelease` в `.csproj`) раскладывает DLL и `.mmdb`
в `addons/counterstrikesharp/plugins/NotifyMessages/` и зипует — архив распаковывается
прямо в корень игрового сервера.

Таргет `DownloadGeoLite2` качает свежие базы MaxMind, если задан `MAXMIND_LICENSE_KEY`
(env) или свойство `GeoLiteLicenseKey`. Без ключа — молчаливый фолбэк на закоммиченные
файлы в `GeoIP/`. **`Directory.Build.props` с реальным ключом в git не попадает**
(см. `Directory.Build.props.example` и `.gitignore`).

## Архитектура

`NotifyMessages` — `partial class : BasePlugin`, разнесённый по файлам:

| Файл | Что в нём |
|---|---|
| `NotifyMessages.cs` | `Load`/`Unload`, ручная сборка всех сервисов |
| `Events/NotifyMessages.Events.cs` | единственная точка регистрации хендлеров (`RegisterEvents`) |
| `Events/NotifyMessages.PlayerEvents.cs` | connect/disconnect/authorized |
| `Events/NotifyMessages.TeamEvents.cs` | смена команды |
| `Commands/NotifyMessages.Commands.cs` | консольные команды |

DI-контейнера нет: сервисы создаются вручную в `Load()` в фиксированном порядке
(logger → config → geoip → messageProcessor → session → display → serverStatus → advert).
Порядок значим — каждый следующий получает предыдущие в конструктор.

Сервисы не наследуют `BasePlugin` и потому **не имеют доступа к `AddTimer`**: таймер-фабрика
передаётся в них делегатом из `Load()`. Сохраняй этот приём при добавлении новых сервисов,
не тащи `BasePlugin` внутрь `Services/`.

### Поток сообщения

```
Config (шаблон с {ключами})
  → MessageProcessor.ProcessMessage(msg, steamId)
      подстановка LanguageMessages по ISO игрока (GeoIP) или DefaultLang
      → ReplaceMessageTags  ({MAP} {TIME} {SERVERNAME} {PLAYERS} …, MapsName, \n → U+2029)
      → TextFormatter.ReplaceColorTags  ({RED} → ChatColors.Red …)
  → DisplayService.Print(destination, msg, target)  → Chat / Center / CenterHtml / Console
```

`DisplayService.Print(dest, msg, target)`: `target == null` — всем, иначе только этому игроку.
При широковещательной рассылке результат кешируется **по ISO-коду языка**, а не по игроку:
обработка идёт один раз на язык.

## Инварианты, которые легко сломать

- **A2S-опрос идёт в фоновом потоке и не имеет права трогать нативы.** `ServerStatusService`
  запускает `Task.Run`, внутри только UDP, строки и `_serverCache` под `lock`. Правило простое:
  из фона — никаких `Utilities.*`, `ConVar.*`, `NativeAPI.*`, обращений к `CCSPlayerController`.
  Нужен главный поток — `Server.NextFrame(...)`. Именно нарушение этого правила когда-то
  заставило автора вернуть опрос в главный поток через `GetAwaiter().GetResult()`, что
  подвешивало сервер до `timeout+250` мс на каждый адрес.
- **`MessageProcessor.ProcessMessage` — только главный поток** (внутри `ConVar.Find`,
  `NativeAPI.GetMapName`, `Utilities.GetPlayers`).
- **В `Load()` и в конструкторах сервисов нативов быть не должно.** На этом этапе движок ещё
  не поднял глобальные переменные, и любой `Server.*` / `NativeAPI.*` падает с
  `NativeException: Global Variables not initialized yet`, а плагин не грузится вовсе.
  Реальный инцидент: `Server.MaxPlayers` в конструкторе `DisplayService` (отсюда константа
  `MaxSlots = 128` вместо размера от сервера). Нативы можно звать только из событий, команд,
  таймеров и `OnTick` — там движок уже готов. Ловит `ServiceConstructionTests`.
- **Ошибка в одной подсистеме не должна ронять загрузку.** Необязательные части
  (реклама, опрос серверов, восстановление после hot reload) запускаются через
  `SafeRun(...)` в `NotifyMessages.cs`; конфиг читается через `LoadConfigSafely()`,
  который в худшем случае отдаёт пустой `Config` — все секции проверяются на null.
- **Цветовые коды берутся из `ChatColors` CounterStrikeSharp.** Свою таблицу заводить нельзя:
  ровно из-за неё половина тегов до 2.1.0 давала не тот цвет.
- **`css_reload_advert` пересоздаёт часть сервисов.** `MessageProcessor`,
  `ServerStatusService`, `AdvertisementService` создаются заново через фабрики
  `CreateServerStatusService()` / `CreateAdvertisementService()` в `NotifyMessages.cs`;
  `DisplayService` только `Update(...)` — чтобы не потерять per-slot состояние HTML-центра.
  Любой новый сервис, кеширующий `Config`, обязан быть добавлен в `ReloadAdvertConfig`,
  иначе останется на старом конфиге.
- **`css_servers` доступна любому игроку** и дёргает сеть — кулдаун
  (`ServersCommandCooldownSeconds`) и guard от параллельных проходов (`_queryInFlight`)
  снимать нельзя.
- **Игрока получаем ТОЛЬКО через `Utilities.GetPlayers()`.** `Utilities.GetPlayerFromSlot(slot)`
  внутри делает `new CCSPlayerController(EntitySystem.GetEntityByIndex(slot + 1))` **без проверки
  типа сущности**: для освобождённого или переиспользованного индекса вернётся чужая энтити,
  и чтение её полей (`PawnIsAlive`, `SteamID`) уходит по неверным смещениям — сервер падает
  без единой строки в консоли. `GetPlayers()` фильтрует по `IsValid` и `Connected`.
  Если слот всё-таки нужен — сначала `IsValid`, потом всё остальное, и в try/catch.
- **Контроллер нельзя проносить через границу кадра.** В `AddTimer`/`Server.NextFrame`
  захватывай `SteamID`, а игрока ищи заново (`FindConnectedPlayer`). За задержку игрок успевает
  выйти, объект освобождается, и даже обращение к `IsValid` становится чтением чужой памяти.
  Реальный инцидент: welcome-сообщение с `DisplayDelay` держало `CCSPlayerController` 5 секунд.
- **Логи из фонового потока — через `Server.NextFrame`** (`BgDebug`/`BgError` в
  `ServerStatusService`). Логгер пишет в консоль, которую перехватывает сам CSSharp.
- **HTML-центр требует перерисовки каждый тик.** `DisplayService.OnTick` шлёт
  `PrintToCenterHtml` пока не истечёт `HtmlCenterDuration` (null = 5 с); состояние — массив
  по слотам, размер от `Server.MaxPlayers`. Убрать `OnTick` = HTML-сообщения исчезнут мгновенно.
  `_htmlActiveCount` — счётчик активных слотов, ради него `OnTick` выходит мгновенно
  в 99% тиков. Счётчик **пересчитывается по факту** в конце каждого прохода, а не ведётся
  вручную: игрок может отвалиться по таймауту без события disconnect, и его слот иначе
  залипал бы навсегда. `User.SteamId` хранит владельца сообщения — слот переиспользуется
  движком, и без сверки новый игрок увидел бы чужой текст.
- **Цветовые теги заменяются от длинных к коротким** (`TextFormatter.SortedTags`) —
  это фикс конфликта префиксов тегов, порядок сортировки менять нельзя.
- **Числа и даты форматируются через `CultureInfo.InvariantCulture`.** Без него сервер
  с арабской/турецкой локалью выдаёт игрокам другие цифры в `{PLAYERS}`, `{SERVER_PORT}` и т.п.
- **Канал вывода задаётся `MessageType` в конфиге**, маппинг один на всех —
  `DisplayService.ToHudDestination`. Свои switch по `MessageType` не плодить.
- **`SessionService` и кеш `ServerStatusService` — под `lock`.** К ним обращаются
  колбэки таймеров и продолжения A2S; блокировки не убирать.
- `ServerStatusService.GetSnapshot()` отдаёт **копию** значений — наружу голый словарь
  не отдавать.

## Конфигурация

Четыре файла в `csgo/addons/counterstrikesharp/configs/plugins/NotifyMessages/`:
`Settings.json`, `Messages.json`, `Ads.json`, `Servers.json` (+ генерируемый `README.txt`).
Если нет ни одного — `ConfigService.CreateDefaultConfigs` создаёт все четыре с примерами.

Битый файл **не роняет плагин и не перезаписывается**: `ConfigService.LoadPart<T>` ловит
`JsonException`, пишет в лог файл, строку и позицию ошибки, добавляет файл в `_failedFiles`,
и `ValidateConfig` в конце печатает громкую сводку. Для этого файла берутся значения
по умолчанию, остальные читаются как обычно. Trailing commas и `//`-комментарии разрешены
осознанно (`ReadOptions`) — это самые частые «ошибки», данные из них читаются однозначно.

Модели в `Models/ConfigModels.cs`: по классу на файл (`SettingsConfig`, `MessagesConfig`,
`AdsConfig`, `ServersConfig`) плюс общий `Config`, который `ConfigService.MergeParts`
склеивает из частей.

**Добавление новой настройки — четыре правки:** поле в частичный конфиг
(напр. `SettingsConfig`) → поле в `Config` → строка в `MergeParts` → значение
в соответствующем `CreateDefault*`. Пропуск `MergeParts` — молчаливый `null` в рантайме.

Тексты и переводы живут **только** в `Messages.json`; `Settings.json` ссылается на них
ключами вида `{prefix}`, `{welcome_player}`. Не хардкодь русский/английский текст
в `Settings.json` и в коде — добавляй ключ в `LanguageMessages`.

## Логирование

`ILogger` → `PluginLogger` (формат `[timestamp] [NotifyMessages] [LEVEL] msg`).
`Debug(...)` печатает только при `Config.Debug == true` (по умолчанию **выключено** — Debug
пишет в лог SteamID, ники и гео игроков). Флаг читается через замыкание, поэтому
подхватывается после перезагрузки конфига. Для статических хелперов есть
`LogService.Current`. `Console.WriteLine` напрямую не использовать.

## Команды плагина

| Команда | Права | Действие |
|---|---|---|
| `css_servers` | CLIENT_ONLY | список серверов из кеша + фоновое обновление, кулдаун 10 с на игрока |
| `css_announce_restart <sec>` | SERVER_ONLY | анонс рестарта, 1–3600 |
| `css_announce_update <sec>` | SERVER_ONLY | анонс обновления, 1–3600 |
| `css_restart_notify <sec>` | SERVER_ONLY | точка интеграции с внешним апдейтером, 0–86400 |
| `css_reload_advert` | `@css/root` | перезагрузка всех четырёх конфигов |

`css_restart_notify` существует, чтобы внешний сервис обновления слал её вместо голого `say`:
текст и цвет берутся из `Settings.RestartNotify` + `Messages.json`, поэтому каждый игрок видит
сообщение на своём языке. Выбор шаблона — `RestartNotifyConfig.ResolveTemplate`: точная отсечка
или `DefaultMessage`; «ближайший» порог намеренно не подбирается.

## Меню в CounterStrikeSharp (разведка 2026-09-03)

Во фреймворке 1.0.369+ встроены только `ChatMenu`, `CenterHtmlMenu`, `ConsoleMenu`
(через `MenuManager.OpenChatMenu/OpenCenterHtmlMenu/OpenConsoleMenu`).
`ScreenMenu`, `WasdMenu`, `PanoramaVote` — это **сторонние** пакеты (CS2ScreenMenuAPI,
CS2MenuManager), в самом CSSharp их нет. Примитив `CPointWorldText` есть — на нём сторонние
библиотеки и строят «экранные» меню.

Для рекламы меню не используем сознательно: меню перехватывает ввод игрока и требует закрытия,
то есть навязчиво посреди раунда. Пассивные каналы — `Chat`, `Center`, `CenterHtml`, `Alert`,
`Console`.

## Версии

**Версию руками нигде не поднимаем.** `ModuleVersion` не литерал: он резолвится из
`AssemblyInformationalVersionAttribute` собранной сборки (`ResolveModuleVersion` /
`FormatModuleVersion` в `NotifyMessages.cs`, хвост `+sha` от SourceLink отрезается).

Источник версии:
- локальная сборка — `<Version>` в `.csproj` (сейчас `2.1.1-fix`), просто база для разработки;
- релиз — **тег**: `release.yml` вычисляет `VERSION=${TAG#v}` и передаёт `-p:Version=` в сборку
  и упаковку, а затем проверяет, что версия реально попала в DLL.

Реальный инцидент, из-за которого так сделано: релиз `v2.1.1` уехал с `ModuleVersion => "v2.1.0"`,
потому что число надо было помнить поднять в двух местах. Возвращать литерал нельзя —
`ModuleVersionTests` это ловит.

## Граф проекта

В `graphify-out/` лежит построенный граф кода (`graph.json`, `graph.html`,
`GRAPH_REPORT.md`). Вопросы вида «что вызывает X», «как связаны Y и Z» быстрее решать
через `graphify query "..."`, чем полным перечитыванием файлов. После заметных изменений
кода — `graphify update`. Каталог в git не коммитится.
