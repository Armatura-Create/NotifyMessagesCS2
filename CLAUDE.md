# CLAUDE.md

**NotifyMessages** — уведомления, реклама, приветствия, локализация и мониторинг чужих
серверов через A2S для CS2. Реализован **дважды**, под две платформы плагинов:

| Каталог | Платформа | Metamod нужен? |
|---|---|---|
| `cssharp/` | CounterStrikeSharp ≥ 1.0.369 | да (CSSharp сам — плагин MM:S) |
| `swiftly/` | SwiftlyS2 ≥ 1.4.9 | **нет**, свой лоадер через `gameinfo.gi` |

**Код продублирован между целями сознательно** (решение владельца: цели развиваются
независимо). Раскладка файлов одинаковая, чтобы цели можно было сравнивать глазами.
Общее у них ровно одно:

> **Формат конфигов — контракт.** `Settings.json`, `Messages.json`, `Ads.json`, `Servers.json`
> переносятся между платформами без правок. Меняешь модель конфига, схему, дефолты или
> семантику тега — меняй в обеих целях или ни в одной. Компилятор об этом не напомнит,
> только этот файл.

Общее по репозиторию: `GeoIP/` (одна копия баз GeoLite2), `LICENSE`, `README*.md`,
`CONTRIBUTING.md`, `.github/`. Репозиторий: <https://github.com/Armatura-Create/NotifyMessagesCS2>.

Файл `.mcp.json` в корне подключает MCP-сервер документации SwiftlyS2
(`https://swiftlys2.net/api/mcp`): `apidocs_lookup`, `docs_search`, `gameevent_lookup` и т.д.
Для вопросов по API SwiftlyS2 — сначала туда, а не в память.

## Цель `cssharp/` — CounterStrikeSharp

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

## Цель `swiftly/` — SwiftlyS2

- `net10.0`, тот же namespace, `SwiftlyS2.CS2` запинен в `swiftly/Swiftly.props`
- SwiftlyS2 — **не надстройка над Metamod**: он подключается строкой `Game csgo/addons/swiftlys2`
  в `gameinfo.gi`. Это альтернативный лоадер, а не аддон.
- То же правило минимальной версии: `MinimumAPIVersion` в `PluginMetadata` обязан равняться
  версии пакета (`ApiVersionTests`). Версия живёт в `Swiftly.props`, потому что нужна и плагину,
  и тестам, а два разъехавшихся литерала превратили бы тест из проверки инварианта в проверку
  внимательности.
- **`PluginMetadata` — атрибут**, а аргументы атрибута обязаны быть константами. MSBuild генерирует
  `PluginVersion.g.cs` (таргет `GeneratePluginVersion`) из `<Version>`, так что правило «версию
  руками не поднимаем» сохраняется.
- **`SwiftlyS2.CS2.dll` — только x64** (выделенный сервер CS2 другим не бывает). На arm64-машине
  она не грузится, поэтому тесты, упоминающие её типы, пропускаются через `SwiftlyRuntime.Available`.
  `Skip.IfNot` в начале тела **не спасает**: JIT разрешает все типы метода до первой строки.
  Такие вызовы живут во вложенных классах `Bound`, а чистые помощники — в `PluginText`.
  CI (x64) не пропускает ничего.
- Сборки SwiftlyS2 идут с `ExcludeAssets="runtime"`: их предоставляет хост, а своя копия рядом
  с плагином — второй `ISwiftlyCore` в процессе. `release.yml` проверяет, что их нет в архиве.
- Каталог конфигов приходит целиком из `Core.Configuration.BasePath`; `.mmdb` лежат в `Core.PluginPath`.
- Команды регистрируются вручную (`Core.Command.RegisterCommand`, `registerRaw: false` → префикс
  `sw_`) и **снимаются в `Unload` по Guid**, иначе после hot reload висят дважды. Хуки событий —
  так же, по Guid. Админское право — строка `notifymessages.admin`; `sw_restart_notify` принимается
  только из консоли (аналог `SERVER_ONLY`).
- `ICommandContext.Args` — только параметры, без имени команды (оно в `CommandName`).
- **HTML-панель держит сам фреймворк**: `IPlayer.SendCenterHTML(html, миллисекунды)`. Ни слотов,
  ни `OnTick`, ни `User` здесь нет, а `Settings.ShowHtmlWhenDead` не действует — пауза на время
  смерти требует перерисовки тиками.
- Таймеры — `Core.Scheduler.DelayAndRepeatBySeconds` / `DelayBySeconds`, отменяются через
  `CancellationTokenSource`; возврат в главный поток из фона — `Core.Scheduler.NextTick`.
- Цвета чата в `TextFormatter` выписаны байтами и **сверены с `ChatColors` CSSharp** (рефлексией
  с пакета 1.0.369): это коды движка. При расхождении верна cssharp-версия.

## Команды

```bash
export PATH="$HOME/.dotnet:$PATH"

cd cssharp && ./build.sh          # restore -> тесты -> Release -> NotifyMessages_cssharp_<version>.zip
cd swiftly && ./build.sh 2.3.0    # то же, с явной версией

dotnet test cssharp/NotifyMessages.sln
dotnet test swiftly/NotifyMessages.sln
```

Тесты лежат в `<цель>/tests/` и покрывают то, что уже ломалось: разбор недоверенных
A2S-пакетов, санацию чужих строк, цветовые теги во всех каналах, префикс цвета в чате,
`GeoIpService.ExtractIp`, ротацию рекламы, диагностику шаблонов, `LanguageIndex`,
`RestartNotifyConfig.ResolveTemplate`, реальное открытие закоммиченной `.mmdb`. Внутренности
открыты тестам через `Properties/AssemblyInfo.cs` (`InternalsVisibleTo`) — публичный API
ради тестов не расширяем.

Каталог `tests/**` исключён из компиляции плагина в `.csproj` — он лежит внутри дерева
проекта, и без `<Compile Remove>` SDK-глоб затянул бы его в саму сборку плагина.

Анализаторы (`EnableNETAnalyzers` + `AnalysisMode=Recommended`) включены постоянно и сборка
держится на нуле предупреждений — именно они поймали форматирование чисел по локали сервера.
`CA1716` и `CA1859` заглушены осознанно в `.csproj`.

## CI/CD

По workflow на цель, каждый с фильтром по путям (`ci-cssharp.yml`, `ci-swiftly.yml`): правка
одной цели не гоняет сборку другой. Обратная сторона: PR, не трогающий цель, не даёт по ней
статуса — такой чек нельзя делать обязательным в branch protection.

`release.yml` срабатывает на тег `v*` (или вручную). Версия вычисляется **один раз** в задаче
`version` и раздаётся обеим целям: два плагина под одним тегом обязаны представляться одним
номером. Каждая цель: build → test → package → проверка архива (нужные файлы есть, конфигов
нет, у swiftly нет сборок SwiftlyS2, версия зашита в DLL) → артефакт. Задача `publish` собирает
описание релиза из коммитов между предыдущим тегом и новым (Conventional Commits, см.
`CONTRIBUTING.md`; коммит не по форме уходит в «Other», а не теряется) и публикует оба архива.
Ей нужен `fetch-depth: 0` — без тегов нечего диффать. Сам релиз создаёт `gh`, а не
`softprops/action-gh-release`: тот грузил оба архива параллельно сразу после создания релиза,
пока релиз ещё не виден по тегу, и падал с `Error saving asset` (так упал первый прогон v2.3.0).
Шаг идемпотентен — повторный запуск дозаливает файлы и обновляет описание.

Красные тесты — релиза нет.

Таргет `DownloadGeoLite2` в обоих `.csproj` качает свежие базы MaxMind, если задан
`MAXMIND_LICENSE_KEY` (env) или свойство `GeoLiteLicenseKey`. Без ключа — молчаливый фолбэк на
закоммиченные файлы в `../GeoIP/`. **`Directory.Build.props` с реальным ключом в git не попадает**
(см. `Directory.Build.props.example` и `.gitignore`).

## Архитектура

Обе цели используют одну раскладку. `NotifyMessages` — `partial class : BasePlugin`,
разнесённый по файлам:

| Файл | Что в нём |
|---|---|
| `NotifyMessages.cs` / `NotifyMessagesPlugin.cs` | `Load`/`Unload`, ручная сборка сервисов, фабрики таймеров |
| `Events/NotifyMessages.Events.cs` | единственная точка регистрации хендлеров (`RegisterEvents`) + `SafeEvent` |
| `Events/NotifyMessages.PlayerEvents.cs` | connect/disconnect, гео, язык, трассировка `[JOIN]`/`[LEAVE]` |
| `Events/NotifyMessages.TeamEvents.cs` | смена команды |
| `Commands/NotifyMessages.Commands.cs` | `*_servers`, `*_restart_notify`, `*_reload_advert` |
| `Commands/NotifyMessages.PreviewCommands.cs` | `*_nm_preview` и `*_nm_check` |
| `Services/IServerInfoSource.cs` + `EngineServerInfo.cs` | факты о сервере для `{MAP}` `{PLAYERS}` …: интерфейс общий, реализация — на фреймворке |
| `Services/MessageProcessor.cs` | локализация, значения, системные теги, рендер под канал — **без фреймворка** |
| `Services/DisplayService.cs` | доставка в чат/центр/HTML/консоль/alert — **на фреймворке** |
| `Services/ServerStatusService.cs`, `AdvertisementService.cs`, `SessionService.cs` | **без фреймворка**: таймер и главный поток приходят делегатами |
| `Utils/TemplateDiagnostics.cs` | чистый анализатор шаблонов (неизвестные теги, дыры в переводах) |
| `Utils/LanguageResolver.cs` | `LanguageIndex`: язык клиента → алиас → страна → `DefaultLang` |

**Что зависит от фреймворка, а что нет — граница проведена намеренно.** Сервисы без
фреймворка идентичны в обеих целях и проверяются тестами на любой машине; в них таймер —
это `Func<float, Action, Action>` («интервал, действие → как остановить»), главный поток —
`Action<Action>`, рассылка — `Action<MessageType, string>`. Не тащи `BasePlugin`, `IPlayer`,
`CCSPlayerController` или `Timer` внутрь `Services/` дальше `DisplayService` и `EngineServerInfo`.

`Services/ConfigService.cs` — только логика загрузки и диагностики; значения по умолчанию
и текст `README.txt` вынесены в `Services/ConfigService.Defaults.cs` (partial, ~650 строк
данных), схемы — в `ConfigService.Schemas.cs`. Правишь дефолты — иди туда, и в обе цели.

DI-контейнера нет: сервисы создаются вручную в `Load()` в фиксированном порядке
(logger → config → geoip → session → languageIndex → serverInfo → messageProcessor → display →
serverStatus → advert). Порядок значим — каждый следующий получает предыдущие в конструктор.

### Поток сообщения

```
Config (шаблон с {ключами})
  → DisplayService.Print(messageType, msg, target, values)
      ResolveChannel: Center + PrintToCenterHtml=true → CenterHtml (совместимость)
      → MessageProcessor.ProcessMessage(msg, steamId, channel, values)
          1. ApplyLanguage    подстановка LanguageMessages по языку игрока
          2. ApplyValues      {PLAYERNAME} {TEAM} {SECONDS} … (для CenterHtml — с экранированием)
          3. ReplaceMessageTags  {MAP} {TIME} {SERVERNAME} {PLAYERS} …, MapsName
          4. Render(channel)  ← у каждого канала своя грамматика
      → PrintToChat / PrintToCenter / PrintToCenterHtml / PrintToConsole / PrintToCenterAlert
```

**Порядок частей 1–4 не произволен.** Значения подставляются ДО рендера, потому что сами содержат
теги (`{TEAM}` = `"{RED}Terrorists{DEFAULT}"`). Пока подстановка шла после `ProcessMessage`,
игроки видели в чате литеральное `{RED}Terrorists{DEFAULT}`. Новые контекстные значения
добавлять только через параметр `values`, не через `.Replace` у вызывающего кода.

`Render` — единственное место, где строка становится специфичной для канала:

| Канал | Цвета | Перенос строки |
|---|---|---|
| `Chat` | `ChatColors` (управляющие байты) | `U+2029` |
| `Center`, `Alert` | теги вырезаются: движок рисует plain-текст | `U+2029` |
| `CenterHtml` | `<font color='#…'>`, размеры `{BIG}/{MEDIUM}/{SMALL}` | `<br>` |
| `Console` | теги вырезаются | настоящий `\n` |

`DisplayService.Print(messageType, msg, target, values)`: `target == null` — всем, иначе только этому игроку.
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
- **Цветовые коды в `cssharp/` берутся из `ChatColors` CounterStrikeSharp.** Свою таблицу
  заводить нельзя: ровно из-за неё половина тегов до 2.1.0 давала не тот цвет. В `swiftly/`
  та же таблица выписана байтами и сверена с `ChatColors` — это копия, а не свой источник.
- **Строка для чата всегда приводится к `"\x01 " + текст`** (`EnsureChatColorPrefix`): движок
  не применяет цвет, стоящий в самом начале сообщения, и CSSharp в своём `ChatMenu` тоже пишет
  пробел перед первым цветом. Ранний выход «уже начинается с кода» — тот самый баг, из-за которого
  `{LIGHTBLUE}Server` выходил белым.
- **Текст, пришедший по сети от чужого сервера, санируется** (`ServerStatusService.SanitizeRemoteText`):
  скобки, управляющие символы, `U+2029` вырезаются, длина ≤ 64. Иначе имя карты `{prefix}{RED}…`
  с чужого сервера красило бы наш чат и давало многострочный спам.
- **`MapsName` подставляется через `MatchEvaluator`, а не строкой замены**: в строке замены
  `Regex.Replace` символ `$` — спецсимвол, и красивое имя карты с долларом превращалось в мусор.
- **`css_reload_advert` пересоздаёт часть сервисов.** `MessageProcessor`,
  `ServerStatusService`, `AdvertisementService` создаются заново через фабрики
  `CreateServerStatusService()` / `CreateAdvertisementService()` в `NotifyMessages.cs`;
  `DisplayService` только `Update(...)` — чтобы не потерять per-slot состояние HTML-центра.
  Любой новый сервис, кеширующий `Config`, обязан быть добавлен в `ReloadAdvertConfig`,
  иначе останется на старом конфиге.
- **`css_servers` доступна любому игроку** и дёргает сеть — кулдаун
  (`ServersCommandCooldownSeconds`) и guard от параллельных проходов (`_queryInFlight`)
  снимать нельзя.
- **Игрока получаем ТОЛЬКО через `Utilities.GetPlayers()` / `Core.PlayerManager` или из события.**
  В `swiftly/` — `ev.UserIdPlayer`, `GetAllPlayers()`, `GetPlayerFromSteamId()`; по номеру слота
  игрока не добываем нигде. Дальше — история из `cssharp/`. `Utilities.GetPlayerFromSlot(slot)`
  внутри делает `new CCSPlayerController(EntitySystem.GetEntityByIndex(slot + 1))` **без проверки
  типа сущности**: для освобождённого или переиспользованного индекса вернётся чужая энтити,
  и чтение её полей (`PawnIsAlive`, `SteamID`) уходит по неверным смещениям — сервер падает
  без единой строки в консоли. `GetPlayers()` фильтрует по `IsValid` и `Connected`.
  **`IsValid` от этого не спасает** — он проверяет указатель, а не тип сущности, поэтому для
  чужой энтити возвращает `true`. Безопасного способа получить игрока по номеру слота нет:
  контроллер берётся из события (`ev.Userid`) или из `GetPlayers()`, и передаётся вниз
  параметром. В плагине не должно оставаться ни одного вызова `GetPlayerFromSlot` —
  это проверяется грепом, а не тестом.
  Второй инцидент того же рода: листенер `Listeners.OnClientAuthorized` доставал игрока
  по слоту ради IP для GeoIP. На авторизации Steam контроллера в слоте может ещё не быть,
  и сервер падал ровно на строке `Client authorized`. Листенер убран, гео кешируется
  в `EventPlayerConnectFull` (`CachePlayerGeo`), где контроллер приходит из события.
  Задержки это не создаёт: анонс входа уходит через 3 с после `ConnectFull`, приветствие —
  через `DisplayDelay`.
- **Базы MaxMind открываются ТОЛЬКО с `FileAccessMode.Memory`.** Дефолтный `MemoryMapped`
  отображает `.mmdb` в память и читает её страничными отказами через
  `SafeMemoryMappedViewHandle`. Внутри игрового процесса это фатально: том Docker/overlayfs
  или движок со своими обработчиками сигналов превращают страничный отказ в SIGBUS, а он
  убивает процесс мгновенно — без исключения, без стека, без строки в логе. Инцидент:
  сервер умирал ровно на `[GEO] 2/5 открываю GeoLite2-Country.mmdb` при полностью целом файле.
  Цена режима `Memory` — RAM размером с базу (Country ~7.8 МБ, City ~58.8 МБ); ошибка чтения
  становится обычным исключением. `GeoIpDatabaseTests` реально открывает закоммиченную базу.
- **Контроллер нельзя проносить через границу кадра.** В `AddTimer`/`Server.NextFrame`
  захватывай `SteamID`, а игрока ищи заново (`FindConnectedPlayer`). За задержку игрок успевает
  выйти, объект освобождается, и даже обращение к `IsValid` становится чтением чужой памяти.
  Реальный инцидент: welcome-сообщение с `DisplayDelay` держало `CCSPlayerController` 5 секунд.
- **Логи из фонового потока — через `Server.NextFrame`** (`BgDebug`/`BgError` в
  `ServerStatusService`). Логгер пишет в консоль, которую перехватывает сам CSSharp.
- **HTML-центр требует перерисовки каждый тик (только `cssharp/`).** `DisplayService.OnTick` шлёт
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
- **Канал вывода — один тип на весь плагин: `MessageType`.** `HudDestination` из сигнатур убран
  осознанно: две системы каналов приводили к тому, что `MessageType.CenterHtml` молча
  превращался в обычный `Center`. Свои switch по `MessageType` плодить только в `Render`
  и `SendToCore`.
- **Подстановка значений — только через `values` в `ProcessMessage`.** `.Replace("{TEAM}", …)`
  после `ProcessMessage` — тот самый баг, из-за которого игроки видели теги текстом.
  Там же единственная точка экранирования: ник игрока — недоверенные данные, и в HTML-панель
  он обязан попадать через `TextFormatter.EscapeHtml`.
- **Цвета в HTML-центре — отдельная hex-таблица `TextFormatter.HtmlColorMap`.** Она НЕ заменяет
  `ChatColors`: чат по-прежнему берёт коды из фреймворка, hex нужен только каналу `CenterHtml`.
- **Все `PrintTo*` бросают `InvalidOperationException`, если сущность стала невалидной.**
  Поэтому `DisplayService.SendTo` и перерисовка HTML в `OnTick` обёрнуты точечным catch:
  пропустить одного получателя дешевле, чем сорвать рассылку или спамить исключением 64 раза
  в секунду.
- **Обработчики событий обёрнуты в `SafeEvent`** (`Events/NotifyMessages.Events.cs`).
  Исключение в нашем хендлере не должно всплывать во фреймворк и мешать другим плагинам.
  У `OnTick` отдельная обёртка: она гасит HTML-центр вместо того, чтобы логировать каждый тик.
- **Состояние игрока чистится в `EventPlayerDisconnect` целиком** — сессия, язык клиента,
  гео-кеш и кулдаун `css_servers`. Любой новый словарь, ключуемый по SteamID, надо добавить
  туда же, иначе он растёт всё время жизни сервера.
- **Язык игрока: клиент → страна → `DefaultLang`.** В `cssharp/` — `player.GetLanguage()`
  (`CounterStrikeSharp.API.Core.Translations`), снимается в `EventPlayerConnectFull` и живёт
  в `SessionService`. В `swiftly/` — **userinfo-квар `cl_language`** через
  `player.GetClientConvarValue("cl_language")` + таблица `SteamLanguage` (копия
  `l_mLanguages` из SwiftlyS2), как в SourceMod. **Не `player.PlayerLanguage`**: SwiftlyS2
  строит его из того же квара, но асинхронно (`QueryClientConvar` при `OnClientPutInServer`)
  и до ответа отдаёт язык *сервера* из `core.jsonc` — `player_connect_full` успевает раньше,
  и снимок на входе получал серверный «en» у всех. Если на входе квар пуст, `ResolveLanguage`
  дочитывает его при первом сообщении и кеширует. GeoIP — фолбэк и источник
  `{COUNTRY}`/`{CITY}`, не более.
  `LanguageIndex` кеширует `Config`, поэтому **обязан** пересобираться в `ReloadAdvertConfig`.
  Словари языков в `MergeParts` пересобираются с `OrdinalIgnoreCase`: движок отдаёт `ru`,
  в конфиге исторически `RU`.
- **`SessionService` и кеш `ServerStatusService` — под `lock`.** К ним обращаются
  колбэки таймеров и продолжения A2S; блокировки не убирать.
- `ServerStatusService.GetSnapshot()` отдаёт **копию** значений — наружу голый словарь
  не отдавать.

## Конфигурация

Четыре файла в `csgo/addons/counterstrikesharp/configs/plugins/NotifyMessages/`:
`Settings.json`, `Messages.json`, `Ads.json`, `Servers.json`.
Если нет ни одного — `ConfigService.CreateDefaultConfigs` создаёт все четыре с примерами.

Рядом лежат `*.schema.json` (`ConfigService.Schemas.cs`) и короткий `README.txt`. Оба
**перезаписываются при каждой загрузке**: пока README писался только при первом запуске,
после обновления плагина он описывал старую версию. Схема — основной способ объяснить конфиг:
редактор с её поддержкой подсказывает поля и подсвечивает опечатки, и она не устаревает молча.
Правишь модель конфига — правь схему в том же коммите.

В сгенерированные конфиги первым свойством вставляется `"$schema"`. Оно не описано в моделях
и `System.Text.Json` его игнорирует — это закреплено тестом, не убирать.

Enum'ы читаются и пишутся строками (`JsonStringEnumConverter`): `"MessageType": "CenterHtml"`.
Числа 0–4 продолжают читаться — старые конфиги не ломаются.

Дефолты обезличены сознательно: плагин ставят чужие люди, и первый запуск не имеет права
включить рекламу чужого Discord. Ссылки в примерах — заглушки (`discord.gg/CHANGE-ME`).

Битый файл **не роняет плагин и не перезаписывается**: `ConfigService.LoadPart<T>` ловит
`JsonException`, пишет в лог файл, строку и позицию ошибки, добавляет файл в `_failedFiles`,
и `ValidateConfig` в конце печатает громкую сводку. Для этого файла берутся значения
по умолчанию, остальные читаются как обычно. Trailing commas и `//`-комментарии разрешены
осознанно (`ReadOptions`) — это самые частые «ошибки», данные из них читаются однозначно.

Модели в `Models/ConfigModels.cs`: по классу на файл (`SettingsConfig`, `MessagesConfig`,
`AdsConfig`, `ServersConfig`) плюс общий `Config`, который `ConfigService.MergeParts`
склеивает из частей.

**Добавление новой настройки — пять правок:** поле в частичный конфиг
(напр. `SettingsConfig`) → поле в `Config` → строка в `MergeParts` → значение
в соответствующем `CreateDefault*` → свойство в соответствующей схеме
(`ConfigService.Schemas.cs`). Пропуск `MergeParts` — молчаливый `null` в рантайме;
пропуск схемы — поле, о котором редактор промолчит.

Тексты и переводы живут **только** в `Messages.json`; `Settings.json` ссылается на них
ключами вида `{prefix}`, `{welcome_player}`. Не хардкодь русский/английский текст
в `Settings.json` и в коде — добавляй ключ в `LanguageMessages`.

## Логирование

`ILogger` → `PluginLogger` (формат `[timestamp] [NotifyMessages] [LEVEL] msg`).
`Debug(...)` печатает только при `Config.Debug == true` (по умолчанию **выключено** — Debug
пишет в лог SteamID, ники и гео игроков). Флаг читается через замыкание, поэтому
подхватывается после перезагрузки конфига. Для статических хелперов есть
`LogService.Current`. `Console.WriteLine` напрямую не использовать.

**Трассировка пути подключения.** `EventPlayerConnectFull` и `EventPlayerDisconnect` печатают
пронумерованные шаги (`[JOIN] 1/8 …`, `[LEAVE] 2/4 …`), `GeoIpService` — свои (`[GEO] 1/5 …`),
включая размер открываемой `.mmdb`. Каждая строка идёт **перед** опасной операцией, а не после:
нарушение памяти в нативном слое убивает процесс без исключения и без стека, и последняя
успевшая напечататься строка — единственное, что говорит, где именно это случилось.
Порядок «лог → операция» не переставлять, иначе трассировка теряет весь смысл.
Диагностируя краш при заходе, включай `Debug` и смотри, на каком номере обрывается лог.

## Команды плагина

| Действие | CounterStrikeSharp | SwiftlyS2 |
|---|---|---|
| список серверов из кеша + фоновое обновление, кулдаун 10 с на игрока | `css_servers` (CLIENT_ONLY) | `sw_servers` (игрок) |
| точка интеграции с внешним апдейтером, 0–86400 | `css_restart_notify <sec>` (SERVER_ONLY) | `sw_restart_notify <sec>` (только консоль) |
| перезагрузка всех четырёх конфигов | `css_reload_advert` (`@css/root`) | `sw_reload_advert` (`notifymessages.admin`) |
| прогон всех шаблонов через `TemplateDiagnostics` | `css_nm_check` (`@css/root`) | `sw_nm_check` (`notifymessages.admin`) |
| рендер шаблона себе: `welcome`, `ad <n>`, `servers`, `key <k>`, `raw <текст>` | `css_nm_preview <цель>` (`@css/root`) | `sw_nm_preview <цель>` (`notifymessages.admin`) |

`css_nm_preview` и `css_nm_check` существуют, чтобы петля «правка конфига → результат» была
секундой, а не интервалом рекламы. Предпросмотр обязан идти через `DisplayService.Print` —
любой обходной путь ничего не доказывает. Предпросмотр рекламы ходит по `ad.Messages` напрямую,
а НЕ через `ad.NextMessages`: тот сдвигает боевую ротацию.

Анонс рестарта и обновления — только `css_restart_notify`. Команды `css_announce_restart`
и `css_announce_update` вместе с полями `RestartMessage`/`UpdateMessage` удалены в v2.2.2:
они умели строго меньше (один текст, всегда в чат) и дублировали `RestartNotify`.

`css_restart_notify` существует, чтобы внешний сервис обновления слал её вместо голого `say`:
текст и цвет берутся из `Settings.RestartNotify` + `Messages.json`, поэтому каждый игрок видит
сообщение на своём языке. Выбор шаблона — `RestartNotifyConfig.ResolveTemplate`: точная отсечка
или `DefaultMessage`; «ближайший» порог намеренно не подбирается.

## Меню: почему не используем (разведка 2026-09-17)

**CounterStrikeSharp 1.0.369–1.0.374**: встроены только `ChatMenu`, `CenterHtmlMenu`, `ConsoleMenu`.
`ScreenMenu`, `WasdMenu`, `PanoramaVote` — сторонние пакеты (CS2MenuManager, CS2ScreenMenuAPI),
то есть ещё один плагин на каждом сервере. В 1.0.374 появился `CustomHudLayout API` — потребует
поднять минимум с 369 до 374 и отрезать серверы между ними; пока не трогаем.

**SwiftlyS2 1.4.9**: встроенный `Core.MenusAPI` (`CreateBuilder()`, `TextMenuOption`,
`AutoCloseAfter`, `FreezePlayer`, `HideFooter`, размеры и цвета через `Design`). Для вывода
сознательно не используется, и вот почему — это факты, а не осторожность:

1. **Одно активное меню на игрока** (`GetCurrentMenu` / `CloseActiveMenu`). Реклама по таймеру
   открыла бы нашу «панель» поверх открытого меню магазина/админки другого плагина — и закрыла его.
2. **Режим ввода серверный** (`core.jsonc` → `InputMode: "button" | "wasd"`), не наш. В `wasd`
   открытое меню перехватывает W/S/E посреди раунда.
3. Это список: `MaxVisibleItems` ≤ 5, пагинация, футер с подсказками. Баннера из него не выходит.
4. Под капотом — та же HTML-панель центра экрана (`MenuOptionTextSize.ToCssClass()` отдаёт те же
   `fontSize-*`). Красота меню — вёрстка, и она доступна в нашем `CenterHtml` напрямую.

Панель CS2 понимает классы `fontSize-xs/s/sm/m/ml/l/xl` (снято со сборки SwiftlyS2). Наши
`{BIG}`/`{MEDIUM}`/`{SMALL}` → `fontSize-l/m/sm`. Пассивные каналы — `Chat`, `Center`,
`CenterHtml`, `Alert`, `Console`; интерактивных нет и не будет.

## Версии

**Версию руками нигде не поднимаем.** `ModuleVersion` не литерал: он резолвится из
`AssemblyInformationalVersionAttribute` собранной сборки (`ResolveModuleVersion` /
`FormatModuleVersion` в `NotifyMessages.cs`, хвост `+sha` от SourceLink отрезается).

Источник версии:
- локальная сборка — `<Version>` в `.csproj` каждой цели (сейчас `2.3.0`), просто база для разработки;
- релиз — **тег**: `release.yml` вычисляет `VERSION=${TAG#v}` один раз и передаёт `-p:Version=`
  в обе сборки, а затем проверяет, что версия реально попала в каждую DLL.

В `swiftly/` `ModuleVersion` заменяет `PluginMetadata.Version` — константа, которую генерирует
MSBuild (`GeneratedVersion.Value`); чистый резолв живёт в `PluginText`.

Реальный инцидент, из-за которого так сделано: релиз `v2.1.1` уехал с `ModuleVersion => "v2.1.0"`,
потому что число надо было помнить поднять в двух местах. Возвращать литерал нельзя —
`ModuleVersionTests` это ловит.

## Граф проекта

В `graphify-out/` лежит построенный граф кода (`graph.json`, `graph.html`,
`GRAPH_REPORT.md`). Вопросы вида «что вызывает X», «как связаны Y и Z» быстрее решать
через `graphify query "..."`, чем полным перечитыванием файлов. После заметных изменений
кода — `graphify update`. Каталог в git не коммитится.
