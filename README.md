# Tunnel2 DNS Server

Авторитативный DNS-сервер для проекта xtunnel2, обрабатывающий A и TXT записи в двух режимах: legacy (стабильный) и new (разработка).

## Обзор

Tunnel2.DnsServer — это специализированный DNS-сервер, написанный на .NET 8, который обеспечивает разрешение доменных имен для туннельных подключений. Сервер поддерживает два формата доменных имён:

### Режим Legacy (стабильный)

**Формат:** `{guid}.{domain}`

**Регулярное выражение:**
```regex
^(?<guid>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\.(?<domain>[-a-z0-9.]+)$
```

**Принцип работы:**
- Все запросы по шаблону `{guid}.tunnel4.com` возвращают один статический IP-адрес
- По умолчанию: `203.0.113.42` (настраивается через `LegacyModeOptions.LegacyStaticIpAddress`)
- TTL: 300 секунд
- Используется для совместимости с существующей инфраструктурой

**Пример:**
```bash
dig A 2a3be342-60f3-48a9-a2c5-e7359e34959a.tunnel4.com @127.0.0.1
# Ответ: 203.0.113.42
```

### Режим New (в разработке)

**Формат:** `{address}-{proxyEntryId}.{domain}`

**Регулярное выражение:**
```regex
^(?<address>[a-z0-9-]{3,128})-(?<proxyEntryId>[a-z0-9][a-z0-9-]{0,31})\.(?<domain>[-a-z0-9.]+)$
```

**Принцип работы:**
- Домен разбивается на части: адрес (vanity name) и идентификатор прокси-точки входа
- IP-адрес определяется по `proxyEntryId` из конфигурации `EntryIpAddressMapOptions.Map`
- TTL: 30 секунд (для динамических обновлений)
- Планируется интеграция с RabbitMQ и Redis для real-time обновления маппинга

**Примеры:**
```bash
dig A my-app-e1.tunnel4.com @127.0.0.1
# Ответ: 203.0.113.10 (IP для entry "e1")

dig A test-service-e2.tunnel4.com @127.0.0.1
# Ответ: 203.0.113.11 (IP для entry "e2")
```

### Поддержка ACME Challenge (TXT записи)

Сервер поддерживает TXT-записи для ACME DNS-01 challenge (Let's Encrypt):

**Формат:** `_acme-challenge.{domain}`

**Принцип работы:**
- Записи хранятся в in-memory словаре (текущая реализация)
- TTL: 60 секунд
- Если запись не найдена → возвращается NXDOMAIN
- В будущем: CRUD API для управления записями через HTTP

**Пример:**
```bash
dig TXT _acme-challenge.my-app-e1.tunnel4.com @127.0.0.1
# Текущая реализация: NXDOMAIN (записи пока не добавлены)
```

## Архитектура

```
┌─────────────────────────────────────────────────────────────┐
│                     DNS Client (dig/resolver)                │
└────────────────────┬────────────────────────────────────────┘
                     │ UDP/TCP :53
                     ▼
┌─────────────────────────────────────────────────────────────┐
│                   Tunnel2.DnsServer                          │
│  ┌──────────────────────────────────────────────────────┐   │
│  │          UdpDnsListener (Port 53/udp)                │   │
│  │          TcpDnsListener (заглушка, порт 53/tcp)      │   │
│  └────────────┬─────────────────────────────────────────┘   │
│               │                                              │
│               ▼                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │           DnsRequestHandler                          │   │
│  │  ┌────────────────────┐  ┌─────────────────────┐    │   │
│  │  │ LegacyMatcher      │  │ NewMatcher          │    │   │
│  │  │ (GUID pattern)     │  │ (address-entryId)   │    │   │
│  │  └────────────────────┘  └─────────────────────┘    │   │
│  │  ┌────────────────────────────────────────────────┐ │   │
│  │  │ ACME Challenge Store (in-memory Dict)         │ │   │
│  │  └────────────────────────────────────────────────┘ │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │       Health Check Endpoint (:8080/healthz)          │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘

Будущие интеграции (следующие этапы):
  ┌──────────────┐     ┌──────────┐     ┌────────────────┐
  │  RabbitMQ    │────▶│  Redis   │────▶│ Entry IP Map   │
  │(SessionCreated)│    │ (cache)  │     │  (real-time)   │
  └──────────────┘     └──────────┘     └────────────────┘
         │
         └──────▶ PostgreSQL (EF Core) - конфигурация, логи
```

## Быстрый старт

### Требования

- .NET 8.0 SDK (для разработки)
- Docker и Docker Compose (для запуска в контейнере)
- Права на биндинг к порту 53 (требуются root/administrator привилегии или CAP_NET_BIND_SERVICE)

### Запуск через Docker Compose

```bash
# Клонируйте репозиторий
git clone https://github.com/your-org/tunnel2-dns-server.git
cd tunnel2-dns-server

# Запустите контейнер
docker-compose up -d

# Проверьте логи
docker-compose logs -f

# Проверьте health check
curl http://localhost:8080/healthz
```

### Запуск через Docker (без compose)

```bash
# Соберите образ
docker build -t tunnel2-dns-server .

# Запустите контейнер с host network (Linux)
docker run -d \
  --name tunnel2-dns-server \
  --network host \
  -e ASPNETCORE_ENVIRONMENT=Production \
  tunnel2-dns-server

# Или с портами (требуется CAP_NET_BIND_SERVICE)
docker run -d \
  --name tunnel2-dns-server \
  -p 53:53/udp \
  -p 53:53/tcp \
  -p 8080:8080/tcp \
  --cap-add=NET_BIND_SERVICE \
  tunnel2-dns-server
```

### Запуск в Windows как Service

```bash
# Соберите проект
dotnet publish src/Tunnel2.DnsServer/Tunnel2.DnsServer.csproj -c Release -o C:\Services\Tunnel2DnsServer

# Установите как Windows Service (требуется PowerShell с правами администратора)
sc.exe create Tunnel2DnsServer binPath="C:\Services\Tunnel2DnsServer\Tunnel2.DnsServer.exe" start=auto
sc.exe description Tunnel2DnsServer "Tunnel2 DNS Server - Authoritative DNS for xtunnel2"
sc.exe start Tunnel2DnsServer

# Проверьте статус
sc.exe query Tunnel2DnsServer

# Логи доступны в Event Viewer или настройте Serilog для файлового логирования
```

**Примечание:** Для Windows потребуется настроить firewall правила для UDP/TCP порта 53.

### Запуск для разработки

```bash
# Восстановите зависимости
dotnet restore

# Запустите сервер (требуется sudo/administrator для порта 53)
sudo dotnet run --project src/Tunnel2.DnsServer/Tunnel2.DnsServer.csproj

# Для разработки без sudo - измените порт в appsettings.Development.json:
# "UdpPort": 5353

# Запустите тесты
dotnet test
```

## Конфигурация

Основная конфигурация задаётся через `appsettings.json`:

```json
{
  "DnsServerOptions": {
    "ListenIpv4": "0.0.0.0",
    "UdpPort": 53,
    "TcpPort": 53,
    "AuthoritativeZones": ["tunnel4.com"],
    "ResponseTtlSeconds": {
      "LegacyA": 300,
      "NewA": 30,
      "Txt": 60,
      "Negative": 5
    }
  },
  "LegacyModeOptions": {
    "IsEnabled": true,
    "LegacyStaticIpAddress": "203.0.113.42"
  },
  "EntryIpAddressMapOptions": {
    "Map": {
      "e1": "203.0.113.10",
      "e2": "203.0.113.11"
    }
  }
}
```

### Переопределение через переменные окружения

```bash
export DnsServerOptions__ListenIpv4="0.0.0.0"
export DnsServerOptions__UdpPort="53"
export LegacyModeOptions__IsEnabled="true"
export LegacyModeOptions__LegacyStaticIpAddress="203.0.113.42"
export EntryIpAddressMapOptions__Map__e1="203.0.113.10"
export EntryIpAddressMapOptions__Map__e2="203.0.113.11"
```

## ACME Challenge / HashiCorp Vault Integration

Сервер поддерживает TXT-записи для ACME DNS-01 challenge (Let's Encrypt) с возможностью хранения токенов в HashiCorp Vault или локальных настройках.

### Принцип работы

1. **Приоритет источников:** Vault → appsettings.json
2. **Hot reload:** Изменения в `appsettings.json` применяются без перезапуска (благодаря `IOptionsMonitor`)
3. **Fallback:** Если Vault недоступен или отключен → используется `appsettings.json`
4. **Запрос на лету:** При каждом DNS-запросе `_acme-challenge.tunnel4.com` токены читаются из текущей конфигурации

### Конфигурация в appsettings.json

```json
{
  "AcmeOptions": {
    "AcmeChallenge1": "",
    "AcmeChallenge2": "",
    "Ttl": "00:01:00"
  },
  "VaultOptions": {
    "Enabled": false,
    "Address": "",
    "Token": "",
    "MountPoint": "secret",
    "Path": "tunnel/tunnel2-dns/acme/wildcard"
  }
}
```

**Параметры AcmeOptions:**
- `AcmeChallenge1` — первый ACME challenge токен (строка)
- `AcmeChallenge2` — второй ACME challenge токен (Let's Encrypt требует 2 для wildcard)
- `Ttl` — Time-To-Live для TXT записей в формате `TimeSpan` (например, `"00:01:00"` = 1 минута)

**Параметры VaultOptions:**
- `Enabled` — включить/выключить интеграцию с Vault (по умолчанию `false`)
- `Address` — адрес Vault сервера (например, `"http://127.0.0.1:8200"`)
- `Token` — токен аутентификации Vault
- `MountPoint` — точка монтирования KV v2 (по умолчанию `"secret"`)
- `Path` — путь к секрету внутри mount point (по умолчанию `"tunnel/tunnel2-dns/acme/wildcard"`)

### Настройка Vault

#### 1. Создание секрета в Vault (KV v2)

```bash
vault kv put secret/tunnel/tunnel2-dns/acme/wildcard \
  AcmeChallenge1="6JQ-Ifh_323tKjxD4DrBLZRgCtbZqbEPx_Xg7Jazu-U" \
  AcmeChallenge2="MWlqf05uyv5KHGO_ZOmVyvUW_sCt0v1PTDrOq7suBHQ" \
  Ttl="00:01:00"
```

#### 2. Проверка секрета

```bash
vault kv get secret/tunnel/tunnel2-dns/acme/wildcard
```

Вывод:
```
====== Data ======
Key               Value
---               -----
AcmeChallenge1    6JQ-Ifh_323tKjxD4DrBLZRgCtbZqbEPx_Xg7Jazu-U
AcmeChallenge2    MWlqf05uyv5KHGO_ZOmVyvUW_sCt0v1PTDrOq7suBHQ
Ttl               00:01:00
```

#### 3. Включение Vault в appsettings.json

```json
{
  "VaultOptions": {
    "Enabled": true,
    "Address": "http://127.0.0.1:8200",
    "Token": "hvs.CAESIJ...",
    "MountPoint": "secret",
    "Path": "tunnel/tunnel2-dns/acme/wildcard"
  }
}
```

#### 4. Проверка через DNS

```bash
dig TXT _acme-challenge.tunnel4.com @127.0.0.1

# Ожидаемый ответ (2 TXT записи):
;; ANSWER SECTION:
_acme-challenge.tunnel4.com. 60 IN TXT "6JQ-Ifh_323tKjxD4DrBLZRgCtbZqbEPx_Xg7Jazu-U"
_acme-challenge.tunnel4.com. 60 IN TXT "MWlqf05uyv5KHGO_ZOmVyvUW_sCt0v1PTDrOq7suBHQ"
```

### Обновление токенов без перезапуска

**Через Vault:**
```bash
# Обновить токены в Vault
vault kv put secret/tunnel/tunnel2-dns/acme/wildcard \
  AcmeChallenge1="new-token-1" \
  AcmeChallenge2="new-token-2" \
  Ttl="00:02:00"

# DNS-сервер автоматически подтянет новые значения при следующем запросе
```

**Через appsettings.json (если Vault отключен):**
```bash
# Отредактировать appsettings.json
vim appsettings.json

# Изменения применятся автоматически в течение нескольких секунд
# Перезапуск НЕ требуется благодаря reloadOnChange: true
```

### Работа в разных окружениях

**Linux/Production (с Vault):**
```json
{
  "VaultOptions": {
    "Enabled": true,
    "Address": "https://vault.example.com",
    "Token": "hvs.CAESIJ..."
  }
}
```

**Windows/Development (без Vault):**
```json
{
  "AcmeOptions": {
    "AcmeChallenge1": "local-test-token-1",
    "AcmeChallenge2": "local-test-token-2",
    "Ttl": "00:01:00"
  },
  "VaultOptions": {
    "Enabled": false
  }
}
```

### Логирование

При запуске с Vault вы увидите:
```
info: Tunnel2.DnsServer.Services.VaultBackedAcmeTokensProvider[0]
      Vault client initialized successfully. Address: http://127.0.0.1:8200
```

При DNS-запросе:
```
info: Tunnel2.DnsServer.Services.VaultBackedAcmeTokensProvider[0]
      Returning 2 ACME token(s) from Vault
info: Tunnel2.DnsServer.Services.DnsRequestHandler[0]
      ACME challenge request for _acme-challenge.tunnel4.com, returning 2 token(s)
```

При fallback на appsettings.json:
```
warn: Tunnel2.DnsServer.Services.VaultBackedAcmeTokensProvider[0]
      Failed to read ACME tokens from Vault, falling back to appsettings.json
```

### Формат данных в Vault

**Важно:** Имена полей в Vault должны точно совпадать с именами в коде:
- `AcmeChallenge1` (не `acme_challenge_1` или `challenge1`)
- `AcmeChallenge2`
- `Ttl` (формат TimeSpan: `"HH:MM:SS"` или `"DD.HH:MM:SS"`)

**Пример с разными TTL:**
```bash
# 30 секунд
vault kv put secret/tunnel/tunnel2-dns/acme/wildcard Ttl="00:00:30"

# 5 минут
vault kv put secret/tunnel/tunnel2-dns/acme/wildcard Ttl="00:05:00"

# 1 час
vault kv put secret/tunnel/tunnel2-dns/acme/wildcard Ttl="01:00:00"
```

## Тестирование

### Запуск unit-тестов

```bash
dotnet test --verbosity normal
```

### Тестирование с помощью dig

```bash
# Legacy format (GUID)
dig A 2a3be342-60f3-48a9-a2c5-e7359e34959a.tunnel4.com @127.0.0.1

# Expected response:
# ;; ANSWER SECTION:
# 2a3be342-60f3-48a9-a2c5-e7359e34959a.tunnel4.com. 300 IN A 203.0.113.42

# New format (address-entryId)
dig A my-app-e1.tunnel4.com @127.0.0.1

# Expected response:
# ;; ANSWER SECTION:
# my-app-e1.tunnel4.com. 30 IN A 203.0.113.10

dig A another-service-e2.tunnel4.com @127.0.0.1

# Expected response:
# ;; ANSWER SECTION:
# another-service-e2.tunnel4.com. 30 IN A 203.0.113.11

# TXT record (ACME challenge)
dig TXT _acme-challenge.test.tunnel4.com @127.0.0.1

# Expected response (текущая реализация):
# ;; status: NXDOMAIN (записи пока не добавлены)

# Запрос вне authoritative zone
dig A example.com @127.0.0.1

# Expected response:
# ;; status: REFUSED

# Несуществующий домен в authoritative zone
dig A nonexistent.tunnel4.com @127.0.0.1

# Expected response:
# ;; status: NXDOMAIN
```

## Стиль кода и нейминг

**Важное правило:** В проекте **НЕ используются сокращённые имена** переменных, полей и свойств.

❌ **Неправильно:**
```csharp
var cfg = new DnsServerOpts();
var svc = new DnsReqHandler();
string ip = opts.LegacyIp;
```

✅ **Правильно:**
```csharp
DnsServerOptions configuration = new DnsServerOptions();
DnsRequestHandler requestHandler = new DnsRequestHandler();
string ipAddress = options.LegacyStaticIpAddress;
```

Это правило закреплено в `.editorconfig` и проверяется при сборке.

## Дорожная карта

### Этап 1: ✅ Минимальный рабочий прототип (текущая реализация)
- [x] Legacy mode с статическим IP
- [x] Базовый new mode с маппингом из конфига
- [x] UDP listener для DNS запросов
- [x] Заглушка TXT записей (in-memory)
- [x] Health check endpoint
- [x] Unit тесты
- [x] Docker образ и CI/CD

### Этап 2: 🔄 Интеграция с PostgreSQL и Entity Framework Core
- [ ] Модели данных для конфигурации (домены, зоны, IP маппинги)
- [ ] DbContext и миграции
- [ ] CRUD API для управления записями
- [ ] Логирование DNS-запросов в БД

### Этап 3: 🔄 RabbitMQ и Redis интеграция
- [ ] Подписка на события `SessionCreated` из RabbitMQ
- [ ] Real-time обновление маппинга `proxyEntryId → IP`
- [ ] Redis кэш для быстрого доступа к маппингу
- [ ] Graceful failover при недоступности Redis

### Этап 4: 🔄 Vanity Names и кастомные домены
- [ ] Таблица `VanityNames` (пользовательские имена → session GUID)
- [ ] Поддержка кастомных доменов пользователей
- [ ] CNAME записи для custom domains
- [ ] Валидация ownership через TXT записи

### Этап 5: 🔄 TCP поддержка и DNSSEC
- [ ] Полноценный TCP listener (для больших ответов)
- [ ] DNSSEC подпись ответов (опционально)

### Этап 6: 🔄 Мониторинг и observability
- [ ] Prometheus метрики (запросы, latency, cache hit rate)
- [ ] OpenTelemetry трейсинг
- [ ] Structured logging (Serilog → Elasticsearch)

## Ссылки

- **Предыдущая/текущая реализация DNS (参考):** [../xtunnel/dns-server](../xtunnel/dns-server)
- **Документация .NET 8:** https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8
- **RFC 1035 (DNS Protocol):** https://www.rfc-editor.org/rfc/rfc1035
- **RFC 8555 (ACME):** https://www.rfc-editor.org/rfc/rfc8555

## Лицензия

MIT License - см. файл [LICENSE](LICENSE)

## Контакты и вклад

Создайте issue или pull request для предложений и исправлений.

---

**Замечание о производительности:**
Текущая реализация использует простой UDP listener с fire-and-forget обработкой запросов. В production окружении рекомендуется:
- Настроить rate limiting для защиты от DNS amplification атак
- Использовать connection pooling для Redis и PostgreSQL
- Настроить мониторинг и алертинг для критических метрик
