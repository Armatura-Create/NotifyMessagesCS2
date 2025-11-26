# NotifyMessages (CS2)

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-API-1f6feb?logo=steam)](https://github.com/roflmuffin/CounterStrikeSharp)
[![Platforms](https://img.shields.io/badge/Platforms-Linux%20%7C%20Windows-2ea44f)](#)
[![Release](https://img.shields.io/badge/Release-ZIP%20package-success)](#build-и-упаковка)
[![GeoLite2](https://img.shields.io/badge/GeoLite2-Auto--download-009688)](#geolite2-данные-автозагрузка-при-сборке)

Универсальный плагин для CounterStrikeSharp/CS2 для уведомлений и рекламы: сообщения в чат, центр экрана (в т.ч. HTML), консоль, а также анонсы других серверов через A2S-запросы.

## ✨ Особенности

- 🌍 **Мультиязычность** — автоматическое определение языка по GeoIP (5+ языков)
- 🎨 **Цветные сообщения** — поддержка 15+ цветовых тегов
- 📱 **Множество каналов вывода** — чат, центр экрана, HTML-центр, консоль
- 🔄 **Модульная конфигурация** — 4 отдельных конфига для удобства
- 🖥️ **Мониторинг серверов** — A2S-запросы с асинхронным кешированием
- ⚡ **Высокая производительность** — кеширование сообщений, оптимизированные регулярные выражения
- 🔒 **Thread-safe** — безопасная работа в многопоточной среде
- 🚀 **Фоновые операции** — все запросы к серверам выполняются асинхронно, без блокировки UI

## 📦 Установка

1. Установите [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) и Metamod:Source
2. Скачайте собранный архив `NotifyMessages.zip` из релизов или соберите проект сами
3. Распакуйте содержимое архива в корень игрового сервера

В архиве уже готова структура:
```
addons/counterstrikesharp/plugins/NotifyMessages/
├── NotifyMessages.dll
├── NotifyMessages.pdb
├── NotifyMessages.deps.json
├── MaxMind.GeoIP2.dll
├── MaxMind.Db.dll
├── GeoLite2-Country.mmdb
└── GeoLite2-City.mmdb
```

4. Запустите сервер — плагин автоматически создаст конфигурационные файлы

## ⚙️ Конфигурация

Плагин использует **модульную систему конфигурации** — 4 отдельных JSON-файла в папке:
```
csgo/addons/counterstrikesharp/configs/plugins/NotifyMessages/
├── Settings.json    # Основные настройки плагина
├── Messages.json    # Все переводы и текстовые сообщения
├── Ads.json         # Рекламные объявления
├── Servers.json     # Список серверов для мониторинга
└── README.txt       # Подробная документация по конфигам
```

**При первом запуске** плагин автоматически создаёт все 4 файла с детальными примерами + `README.txt` с полной документацией!

После редактирования используйте команду `css_reload_advert` для применения изменений без перезапуска сервера.

---

### 📄 Settings.json — Основные настройки

**Назначение:** Базовые параметры плагина, приветственные сообщения, ссылки на ключи переводов.

#### Структура:

```json
{
  "Debug": true,
  "DefaultLang": "RU",
  "PrintToCenterHtml": false,
  "ShowHtmlWhenDead": null,
  "HtmlCenterDuration": null,
  
  "WelcomeMessage": {
    "MessageType": 0,
    "Message": "{prefix}{welcome_player} {RED}{PLAYERNAME} {DEFAULT}{welcome_text}",
    "DisplayDelay": 5
  },
  
  "RestartMessage": "{prefix}{RED}{will_restarted}",
  "UpdateMessage": "{prefix}{RED}{will_updated}",
  "ChangeTeamMessage": "{prefix}{changeTeamMessage}",
  "JoinTeamMessage": "{prefix}{joinTeamMessage}",
  "TitleAnnounceServers": "{prefix}{announce_servers}",
  
  "MapsName": {
    "de_dust2": "Dust 2",
    "de_mirage": "Mirage",
      "PL": "na serwer gry {RED}Armaturix",
      "DE": "auf den Spieleserver {RED}Armaturix"
    },
    "de_inferno": "Inferno",
    "de_nuke": "Nuke",
    "de_overpass": "Overpass"
  }
}
```

#### Параметры:

| Параметр | Тип | Описание |
|----------|-----|----------|
| `Debug` | bool | Включить подробное логирование (true/false) |
| `DefaultLang` | string | Язык по умолчанию (RU/US/UA/PL/DE) |
| `PrintToCenterHtml` | bool? | Использовать HTML для центральных сообщений |
| `ShowHtmlWhenDead` | bool? | Показывать HTML мёртвым игрокам |
| `HtmlCenterDuration` | float? | Длительность показа HTML в секундах |
| `WelcomeMessage` | object | Приветственное сообщение при подключении |
| `RestartMessage` | string | Шаблон сообщения о рестарте (использует ключи из Messages.json) |
| `UpdateMessage` | string | Шаблон сообщения об обновлении |
| `ChangeTeamMessage` | string | Шаблон при смене команды |
| `JoinTeamMessage` | string | Шаблон при входе в команду |
| `TitleAnnounceServers` | string | Заголовок для команды !servers |
| `MapsName` | object | Красивые названия карт (технич. название → отображаемое) |

**💡 Важно:** В сообщениях используются ключи типа `{prefix}`, `{welcome_player}` и т.д. — все переводы находятся в **Messages.json**!

#### WelcomeMessage:

```json
{
  "MessageType": 0,      // 0=Chat, 1=Center, 2=CenterHtml, 3=Console
  "Message": "...",      // Шаблон с ключами из Messages.json
  "DisplayDelay": 5      // Задержка показа в секундах
}
```

---

### 🌍 Messages.json — Переводы и сообщения

**Назначение:** Централизованное хранилище всех текстов и переводов на разные языки.

#### Структура:

```json
{
  "LanguageMessages": {
    "ключ": {
      "RU": "Русский текст",
      "US": "English text",
      "UA": "Український текст",
      "PL": "Polski tekst",
      "DE": "Deutscher Text"
    }
  },
  "JoinMessages": { ... },
  "LeaveMessages": { ... }
}
```

#### LanguageMessages — Переводы:

Здесь находятся ВСЕ переводимые тексты плагина. Примеры ключей:

- `prefix` — префикс для всех сообщений
- `welcome_player` — приветствие игрока
- `welcome_text` — текст приветствия
- `reklama_1`, `reklama_2`, ... — тексты рекламы
- `will_restarted`, `will_updated` — системные сообщения
- `changeTeamMessage`, `joinTeamMessage` — сообщения о командах
- `player`, `connected`, `disconnected` — статусы игроков
- `announce_servers` — заголовок списка серверов

Полный список смотрите в автоматически созданном файле после первого запуска!

#### JoinMessages / LeaveMessages:

Массивы сообщений для показа при подключении/отключении игроков:

```json
{
  "JoinMessages": {
    "RU": [
      "{player} {connected} Страна: {country}, Город: {city}",
      "{connected} Игрок {PLAYERNAME} из {COUNTRY}!"
    ],
    "US": [
      "{player} {connected} Country: {country}, City: {city}"
    ]
  }
}
```

Плагин случайно выбирает одно из сообщений для языка игрока.

**Доступные плейсхолдеры:**
- `{PLAYERNAME}` — имя игрока
- `{COUNTRY}` — страна (через GeoIP)
- `{CITY}` — город (через GeoIP)
- `{player}`, `{connected}`, `{disconnected}` — ключи переводов из LanguageMessages

---

### 📢 Ads.json — Реклама

**Назначение:** Циклические рекламные объявления с настраиваемыми интервалами.

#### Пример:

```json
{
  "Ads": [
    {
      "Interval": 120,
      "Messages": [
        { "Chat": "{prefix}{reklama_1}" },
        { "Center": "!ws • !knife • !gloves • !skins" }
      ]
    },
    {
      "Interval": 180,
      "Messages": [
        { "Chat": "{prefix}{reklama_2}" },
        { "Center": "!viptest - FREE!" }
      ]
    },
    {
      "Interval": 240,
      "Messages": [
        { "Chat": "{prefix}{reklama_3}" }
      ]
    }
  ]
}
```

#### Структура рекламного блока:

| Параметр | Описание |
|----------|----------|
| `Interval` | Интервал показа в секундах (минимум 1) |
| `Messages` | Массив сообщений (показываются циклически) |

#### Каналы вывода:

- `"Chat"` — в чат
- `"Center"` — в центр экрана
- `"Console"` — в консоль игрока

Можно комбинировать несколько каналов в одном сообщении!

**💡 Совет:** Ключи типа `{reklama_1}`, `{reklama_2}` берутся из **Messages.json** → `LanguageMessages`

---

### 🖥️ Servers.json — Мониторинг серверов

**Назначение:** Отображение статуса других серверов через A2S-протокол.

#### Пример:

```json
{
  "Enabled": true,
  "Interval": 60,
  "QueryTimeoutMs": 500,
  "CacheTtlSeconds": 30,
  
  "List": [
    {
      "Ip": "123.45.67.89",
      "Port": 27015,
      "MessageTemplate": "{LIGHTBLUE}[SERVER 1]{DEFAULT} {SERVER_MAP} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{SERVER_MAXPLAYERS}",
      "MessageTemplateConsole": "Server 1: {SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
      "MaxPlayersFallback": 32
    },
    {
      "Ip": "123.45.67.90",
      "Port": 27015,
      "MessageTemplate": "{LIGHTBLUE}[SERVER 2]{DEFAULT} {SERVER_MAP} | {GREEN}{SERVER_PLAYERS}{DEFAULT}/{SERVER_MAXPLAYERS}",
      "MessageTemplateConsole": "Server 2: {SERVER_IP}:{SERVER_PORT} - {SERVER_MAP} | {SERVER_PLAYERS}/{SERVER_MAXPLAYERS}",
      "MaxPlayersFallback": 32
    }
  ]
}
```

#### Основные параметры:

| Параметр | Тип | Описание |
|----------|-----|----------|
| `Enabled` | bool | Включить/выключить мониторинг (по умолчанию `false`) |
| `Interval` | float | Интервал автоматического опроса в секундах (минимум 5, рекомендуется 60+) |
| `QueryTimeoutMs` | int | Таймаут A2S-запроса в миллисекундах (200-5000, рекомендуется 500) |
| `CacheTtlSeconds` | int | Время жизни кеша в секундах (0-60, рекомендуется 30) |
| `List` | array | Массив серверов для мониторинга |

#### Параметры сервера:

| Параметр | Описание |
|----------|----------|
| `Ip` | IP-адрес или hostname сервера |
| `Port` | Порт сервера |
| `MessageTemplate` | Шаблон для чата (с цветовыми тегами) |
| `MessageTemplateConsole` | Шаблон для консоли (без цветов) |
| `MaxPlayersFallback` | Макс. игроков если сервер оффлайн |

#### Плейсхолдеры для MessageTemplate:

| Плейсхолдер | Описание |
|-------------|----------|
| `{SERVER_IP}` | IP-адрес сервера |
| `{SERVER_PORT}` | Порт сервера |
| `{SERVER_MAP}` | Текущая карта (или "OFFLINE" если недоступен) |
| `{SERVER_PLAYERS}` | Количество игроков онлайн |
| `{SERVER_MAXPLAYERS}` | Максимум игроков (или MaxPlayersFallback) |

#### Особенности работы:

- ✅ **Синхронные запросы в главном потоке** — полная стабильность, без thread-ошибок
- ✅ **Умное кеширование** — TTL-кеш снижает нагрузку на серверы
- ✅ **Фоновое обновление** — после команды `!servers` кеш обновляется для следующего запроса
- ✅ **Последовательная обработка** — серверы опрашиваются с интервалом 50-100ms
- ⚠️ **Компромисс:** Возможна небольшая задержка сервера при опросе (до 500ms на сервер)

---

## 🎨 Цветовые теги

Плагин поддерживает следующие цветовые теги (автоматически конвертируются в коды CS2):

| Тег | Цвет | Тег | Цвет |
|-----|------|-----|------|
| `{DEFAULT}` / `{WHITE}` | Белый | `{RED}` | Красный |
| `{GREEN}` / `{LIME}` | Зелёный | `{BLUE}` | Синий |
| `{YELLOW}` / `{GOLD}` | Жёлтый | `{ORANGE}` | Оранжевый |
| `{LIGHTBLUE}` | Голубой | `{DARKBLUE}` | Тёмно-синий |
| `{PURPLE}` / `{LIGHTPURPLE}` | Фиолетовый | `{MAGENTA}` | Пурпурный |
| `{GREY}` / `{GRAY}` | Серый | `{SILVER}` | Серебряный |
| `{DARKRED}` | Тёмно-красный | `{OLIVE}` | Оливковый |
| `{LIGHTYELLOW}` | Светло-жёлтый | `{BLUEGREY}` | Сине-серый |

**Дополнительные теги:**
- `{SPACE}` — широкий пробел для выравнивания
- `\n` — перенос строки (автоматически конвертируется в `\u2029`)

---

## 📝 Системные плейсхолдеры

Доступны во всех сообщениях:

| Плейсхолдер | Описание | Пример |
|-------------|----------|--------|
| `{MAP}` | Название карты | de_dust2 или DUST 2 (если в MapsName) |
| `{TIME}` | Текущее время | 15:30:45 |
| `{DATE}` | Текущая дата | 26.11.2024 |
| `{SERVERNAME}` | Имя сервера | Мой CS2 Сервер |
| `{IP}` | IP сервера | 192.168.1.100 |
| `{PORT}` | Порт сервера | 27015 |
| `{MAXPLAYERS}` | Макс. слотов | 32 |
| `{PLAYERS}` | Игроков онлайн | 18 |
| `{TIME_RESTART}` | Время до рестарта | 05:00 (в командах) |

---

## 🎮 Команды

### Для игроков:

| Команда | Описание |
|---------|----------|
| `css_servers` | Показать список серверов из кеша |

**Особенность:** После показа списка запускается фоновое обновление кеша, чтобы следующий запрос показал актуальные данные.

### Для администраторов:

| Команда | Права | Описание |
|---------|-------|----------|
| `css_announce_restart <сек>` | SERVER_ONLY | Объявить рестарт через N секунд (1-3600) |
| `css_announce_update <сек>` | SERVER_ONLY | Объявить обновление через N секунд (1-3600) |
| `css_reload_advert` | @css/root | Перезагрузить все 4 конфигурации без перезапуска |

**Важно:** Команды `css_announce_restart` и `css_announce_update` имеют ограничение **от 1 до 3600 секунд** (1 час) для безопасности.

#### Примеры:

```
css_announce_restart 300     // Рестарт через 5 минут
css_announce_update 60       // Обновление через 1 минуту
css_reload_advert            // Перезагрузить все конфиги
```

---

## ⚡ Оптимизации и производительность

### Реализованные улучшения:

1. **Кеширование сообщений по языку**
   - Сообщения обрабатываются один раз для каждого языка
   - Если у всех игроков RU язык, обработка происходит только один раз
   - Значительное сокращение CPU нагрузки при большом количестве игроков

2. **Скомпилированные регулярные выражения**
   - Regex для парсинга тегов компилируется один раз при загрузке
   - Значительное ускорение обработки сообщений

3. **Умные A2S-запросы серверов**
   - Запросы выполняются в главном потоке для стабильности
   - Последовательная обработка с интервалами 50-100ms
   - TTL-кеширование снижает частоту запросов
   - Уменьшенный таймаут (500ms) для минимизации задержек

4. **Thread-safe операции**
   - Все критические секции защищены `lock`
   - SessionService безопасен для многопоточного доступа
   - ServerStatusService использует потокобезопасный кеш
   - Полная защита от race conditions

5. **Улучшенное логирование**
   - Временные метки для всех сообщений (HH:mm:ss)
   - Полные Stack Trace для ошибок в Debug режиме
   - Структурированный формат: `[timestamp] [plugin] [level] message`
   - Опциональный вывод обработанных сообщений в консоль

6. **Модульная конфигурация**
   - 4 отдельных файла для разных функций
   - Быстрая загрузка при старте
   - Простая поддержка и редактирование

---

## 🔧 Build и упаковка

### Сборка релиза:

```bash
dotnet build -c Release
```

Готовый архив: `bin/Release/net8.0/NotifyMessages.zip`

Внутри архива уже правильная структура `addons/counterstrikesharp/plugins/NotifyMessages/` с файлами плагина и зависимостями. Архив можно просто распаковать в корень сервера.

---

## 🌍 GeoLite2 данные (автозагрузка при сборке)

Чтобы в релиз попадали актуальные базы `GeoLite2-Country.mmdb` и `GeoLite2-City.mmdb`:

### Способ 1: Переменная окружения (рекомендовано для CI)

**macOS/Linux:**
```bash
export MAXMIND_LICENSE_KEY=ВАШ_КЛЮЧ
dotnet build -c Release
```

**Windows (PowerShell):**
```powershell
setx MAXMIND_LICENSE_KEY "ВАШ_КЛЮЧ"
# Перезапустите терминал/IDE
dotnet build -c Release
```

### Способ 2: Свойство MSBuild

```bash
dotnet build -c Release -p:GeoLiteLicenseKey=ВАШ_КЛЮЧ
```

### Способ 3: Локальный props-файл

Скопируйте `Directory.Build.props.example` в `Directory.Build.props` и укажите ключ. Файл исключён из git.

### Фолбэк

Если ключ не задан или загрузка не удалась, сборка использует локальные файлы:
- Положите `GeoLite2-Country.mmdb` и `GeoLite2-City.mmdb` в папку `GeoIP/` в корне репозитория
- Они будут автоматически скопированы в выходную папку и включены в релизный ZIP

**Получить ключ:** Зарегистрируйтесь на [maxmind.com](https://www.maxmind.com/en/geolite2/signup)

---

## 📚 Примечания

### Локализация

- Плагин автоматически определяет страну игрока по IP через GeoIP2
- Поддерживаются языки: RU, US, UA, PL, DE
- Можно легко добавить новые языки в `LanguageMessages`
- Если язык игрока не найден, используется `DefaultLang`

### Особенности работы

- Приветственные сообщения поддерживают все плейсхолдеры и локализацию
- Сообщения о смене команды локализуются для каждого игрока отдельно
- HTML-центр работает только для живых игроков (если `ShowHtmlWhenDead: false`)
- Анонсы серверов по умолчанию отключены (`Enabled: false`)

### Совместимость

- Минимальная версия CounterStrikeSharp API: 339
- .NET 8.0
- Windows и Linux

### Безопасность

- Все входные данные команд валидируются
- Ограничения на время рестарта/обновления (1-3600 сек)
- Thread-safe операции во всех критических секциях
- Защита от переполнения при A2S-запросах

---

## 🐛 Исправленные баги (v2.0.0)

- ✅ Исправлен расчёт времени HTML-центра (`.Seconds` → `.TotalSeconds`)
- ✅ Удалено дублирование `InitialQuery()` в команде reload
- ✅ Добавлена потокобезопасность в `SessionService`
- ✅ Добавлены ограничения на команды announce (1-3600 сек)
- ✅ Оптимизированы регулярные выражения (скомпилированные)
- ✅ Реализовано кеширование обработанных сообщений
- ✅ Улучшена система логирования (временные метки, stack trace)
- ✅ A2S-запросы теперь полностью асинхронны (не блокируют UI)

---

## 📖 Changelog

### v2.0.0 (2024-11-26)

#### 🚀 Новое
- Кеширование обработанных сообщений по языку
- Асинхронные фоновые A2S-запросы
- Фоновое обновление кеша серверов после команды `css_servers`
- Улучшенное логирование с временными метками

#### ⚡ Оптимизации
- Скомпилированные регулярные выражения
- Thread-safe операции во всех сервисах
- Валидация команд (лимиты 1-3600 секунд)

#### 🐛 Исправления
- Корректный расчёт времени HTML-центра
- Удалено дублирование InitialQuery()
- Исправлена локализация в WelcomeMessage
- Исправлена локализация сообщений о смене команды

---

**Автор:** Armatura  
**Версия:** v2.0.0  
**Лицензия:** MIT  
**GitHub:** [ваша-ссылка-здесь]

---

## 💬 Поддержка

Если у вас возникли вопросы или проблемы:
1. Проверьте логи сервера (включите `Debug: true`)
2. Убедитесь, что файлы GeoIP на месте
3. Проверьте права на команды (@css/root для reload)
4. Создайте Issue на GitHub с подробным описанием
