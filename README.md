# RateLimiter

Middleware для ограничения частоты HTTP-запросов в ASP.NET Core.

## Содержание

- [Обзор](#обзор)
- [Архитектура](#архитектура)
  - [Обработка запроса](#обработка-запроса)
  - [Хранение состояния](#хранение-состояния)
  - [Multi-instance с Redis](#multi-instance-с-redis)
- [Подключение](#подключение)
- [Алгоритмы](#алгоритмы)
  - [Token Bucket](#token-bucket)
  - [Sliding Window](#sliding-window)
- [Политики](#политики)
- [Хранилища](#хранилища)
  - [InMemory](#inmemory)
  - [Redis](#redis)
- [Идентификация клиента](#идентификация-клиента)
- [Обработка 429](#обработка-429)
- [HTTP-заголовки](#http-заголовки)
- [Атрибуты](#атрибуты)
- [Расширение](#расширение)
  - [Свой алгоритм](#свой-алгоритм)
  - [Своё хранилище](#своё-хранилище)
  - [Свой обработчик 429](#свой-обработчик-429)
  - [Свой идентификатор клиента](#свой-идентификатор-клиента)
- [Полный пример](#полный-пример)
- [Зависимости](#зависимости)

---

## Обзор

Библиотека предоставляет middleware, который проверяет каждый входящий HTTP-запрос и решает — пропустить или отклонить с кодом 429.

Два встроенных алгоритма:
- **Token Bucket** — ведро с токенами, пополняемое с заданной скоростью
- **Sliding Window** — скользящее окно с подсчётом запросов

Два типа хранилищ:
- **InMemory** — для одиночных инстансов
- **Redis** — для распределённых развёртываний

Каждый из этих компонентов можно заменить на собственную реализацию через DI.

---

## Архитектура

### Обработка запроса

Каждый HTTP-запрос проходит через middleware в следующем порядке:

```
HTTP Request
    │
    ▼
┌───────────────────────────────┐
│  1. SkipRateLimiting?         │
│     Да → пропустить запрос    │
│     Нет → продолжить          │
└───────────────┬───────────────┘
                │
                ▼
┌───────────────────────────────┐
│  2. Определить политику       │
│     [RateLimitPolicy("x")]    │
│     → если нет → "default"    │
└───────────────┬───────────────┘
                │
                ▼
┌───────────────────────────────┐
│  3. Получить ключ клиента     │
│     IKeyProvider.GetKey()     │
│     → IP / claims / custom    │
└───────────────┬───────────────┘
                │
                ▼
┌───────────────────────────────┐
│  4. Сформировать storage key  │
│     "{policyName}:{clientKey}"│
└───────────────┬───────────────┘
                │
                ▼
┌───────────────────────────────┐
│  5. IDataStorage              │
│     .TryConsumeAsync(key)     │
│                               │
│  Лимитер существует?          │
│  Нет → создать через          │
│       IRateLimiterFactory     │
│  Да → попытаться потратить    │
│       токен/запрос            │
└───────────────┬───────────────┘
                │
        ┌───────┴───────┐
        ▼               ▼
    ┌──────────┐     ┌──────────┐
    │Успех     │     │Отклонён  │
    │          │     │          │
    │Заголов-  │     │Заголовки │
    │ки        │     │          │
    │          │     │429 через │
    │→ next    │     │IRateLimit│
    │middleware│     │Rejection │
    │          │     │Handler   │
    └──────────┘     └──────────┘
```

### Хранение состояния

Лимитер создаётся при первом запросе от клиента по данной политике и хранится в хранилище.

```
InMemory:

┌───────────────────────────────────────────────┐
│  IMemoryCache                                 │
│                                               │
│  "api:192.168.1.1"  → TokenBucket             │
│                        tokens=73              │
│                        lastRefill=10:30:15    │
│                        TTL=1h (sliding)       │
│                                               │
│  "strict:10.0.0.5"  → SlidingWindow           │
│                        requests=[10:29, 10:31]│
│                        TTL=1h (sliding)       │
│                                               │
│  SizeLimit: 10 000 записей                    │
│  При удалении записи → следующий запрос       │
│  получает полный лимит заново                 │
└───────────────────────────────────────────────┘


Redis:

┌──────────────────────────────────────────────┐
│  Redis Database                              │
│                                              │
│  KEY "ratelimiter:api:192.168.1.1"           │
│  VALUE '{"Tokens":73,"LastRefill":"..."}'    │
│  TTL 10 минут                                │
│                                              │
│  KEY "ratelimiter:strict:10.0.0.5"           │
│  VALUE '{"Requests":["...","..."]}'          │
│  TTL 10 минут                                │
│                                              │
│  Записи обновляются только при TryConsume.   │
│  GetRemaining/GetReset — read-only,          │
│  не продлевают TTL.                          │
└──────────────────────────────────────────────┘
```

### Multi-instance с Redis

При нескольких инстансах приложения за load balancer'ом InMemory не подходит — каждый инстанс хранит своё состояние, и клиент с IP 1.2.3.4 может получить 100 токенов на инстансе A и ещё 100 на инстансе B.

Redis решает эту проблему — все инстансы читают и пишут в одну и ту же запись:

```
┌─────────────┐     ┌─────────────┐
│  Instance A │     │  Instance B │
│  (Kestrel)  │     │  (Kestrel)  │
└──────┬──────┘     └──────┬──────┘
       │                   │
       │  WATCH key        │  WATCH key
       │  GET state        │  GET state
       │  Modify limiter   │  Modify limiter
       │  MULTI            │  MULTI
       │  SET state        │  SET state
       │  EXEC             │  EXEC
       │                   │
       ▼                   ▼
┌──────────────────────────────────────┐
│  Redis                               │
│                                      │
│  "ratelimiter:api:1.2.3.4"           │
│  → одно состояние, оба инстанса      │
│    видят одни и те же токены         │
└──────────────────────────────────────┘
```

Для защиты от race condition используется **optimistic locking** (WATCH/MULTI/EXEC). Если между WATCH и EXEC другой инстанс изменил ключ, транзакция откатывается и выполняется повторно (до 10 попыток).

```
Instance A                    Redis                     Instance B
    │                           │                           │
    │── WATCH key ──────────────│                           │
    │── GET key ────────────────│                           │
    │                           │                           │
    │  десериализация           │       WATCH key ──────────│
    │  consume токена           │       GET key ────────────│
    │  сериализация             │                           │
    │                           │       десериализация      │
    │── MULTI ──────────────────│       consume токена      │
    │── SET new_state ──────────│       сериализация        │
    │── EXEC ───────────────────│                           │
    │                           │       MULTI ──────────────│
    │   OK, committed           │       SET new_state ──────│
    │                           │       EXEC ───────────────│
    │                           │                           │
    │                           │       FAIL (key changed)  │
    │                           │       → retry from WATCH  │
```

---

## Подключение

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRateLimiter(options =>
{
    options.AddTokenBucketLimiter("default", tb =>
    {
        tb.TokenLimit = 100;
        tb.TokensPerPeriod = 10;
        tb.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
    });
});

var app = builder.Build();

app.UseRateLimiter();   // ← зарегистрировать после UseRouting(), перед MapControllers()
app.MapControllers();
app.Run();
```

`AddRateLimiter` выбросит `InvalidOperationException` если не зарегистрировано ни одной политики.

---

## Алгоритмы

### Token Bucket

Каждому клиенту выдаётся пул токенов. При запросе расходуется один токен. Если токенов нет — 429. Токены пополняются непрерывно.

```csharp
options.AddTokenBucketLimiter("api", tb =>
{
    tb.TokenLimit = 100;
    tb.TokensPerPeriod = 10;
    tb.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
});
```

| Параметр | Тип | Описание | Ограничение |
|----------|-----|----------|-------------|
| `TokenLimit` | `int` | Максимальная ёмкость ведра | > 0 |
| `TokensPerPeriod` | `int` | Токенов, добавляемых за один `ReplenishmentPeriod` | > 0 |
| `ReplenishmentPeriod` | `TimeSpan` | Интервал пополнения | > 0 |

**Пример:** `TokenLimit=100, TokensPerPeriod=10, ReplenishmentPeriod=1с` — ведро вмещает 100 токенов, пополняется по 10 токенов в секунду. При полном ведре клиент может сделать 100 запросов подряд, потом по 10 запросов в секунду.

**Особенности:**
- Пополнение происходит при каждом вызове `TryConsume` на основе прошедшего времени
- Токены целочисленные — дробные токены отбрасываются
- Емкость никогда не превышает `TokenLimit`

---

### Sliding Window

Каждый запрос фиксируется с временной меткой. При проверке все метки старше `Window` удаляются. Если оставшихся меток >= `RequestsLimit` — 429.

```csharp
options.AddSlidingWindowLimiter("strict", sw =>
{
    sw.RequestsLimit = 200;
    sw.Window = TimeSpan.FromMinutes(1);
});
```

| Параметр | Тип | Описание | Ограничение |
|----------|-----|----------|-------------|
| `RequestsLimit` | `int` | Максимум запросов в окне | > 0 |
| `Window` | `TimeSpan` | Длительность окна | > 0 |

**Пример:** `RequestsLimit=200, Window=1мин` — не более 200 запросов в минуту.

**Особенности:**
- Окно скользящее — нет «тактов», границ нет
- Старые метки удаляются лениво, при следующем запросе
- `GetReset()` возвращает время, когда самый старый запрос «выпадет» из окна

---

## Политики

Политика — именованная конфигурация лимита. Каждая политика связана с одним алгоритмом.

```csharp
options.AddTokenBucketLimiter("api", tb => { ... });       // политика "api" → Token Bucket
options.AddSlidingWindowLimiter("strict", sw => { ... });   // политика "strict" → Sliding Window
```

Одно приложение может иметь несколько политик с разными алгоритмами и параметрами. Каждый эндпоинт использует свою политику через `[RateLimitPolicy("name")]`.

Если атрибут не указан — используется политика `"default"`. Если политика `"default"` не зарегистрирована — `InvalidOperationException` при первом запросе к такому эндпоинту.

---

## Хранилища

### InMemory

Регистрируется по умолчанию. Состояние лимитеров хранится в `IMemoryCache`.

```csharp
// значения по умолчанию
builder.Services.AddRateLimiter(options => { ... });

// с настройкой
builder.Services.AddRateLimiter(options => { ... })
    .UseInMemoryStorage(
        cacheOptions =>
        {
            cacheOptions.SizeLimit = 50_000;
        },
        entryOptions =>
        {
            entryOptions.SlidingExpiration = TimeSpan.FromMinutes(30);
            entryOptions.Size = 1;
        });
```

| Параметр | Значение по умолчанию | Описание |
|----------|----------------------|----------|
| `SizeLimit` | 10 000 | Максимум записей в кэше |
| `SlidingExpiration` | 1 час | TTL записи (сбрасывается при каждом обращении) |
| `Size` | 1 | Размер одной записи в единицах кэша |

При переполнении кэша записи удаляются по LRU. Удалённая запись = клиент получает полный лимит заново.

---

### Redis

Состояние лимитеров хранится в Redis. Сериализуется в JSON.

#### Способ 1: строка подключения

```csharp
builder.Services.AddRateLimiter(options => { ... })
    .UseRedis("localhost:6379");

builder.Services.AddRateLimiter(options => { ... })
    .UseRedis("localhost:6379", db: 5);   // номер БД
```

#### Способ 2: Action<ConfigurationOptions>

Для полного контроля над настройками подключения — пароли, SSL, таймауты, sentinel, cluster:

```csharp
builder.Services.AddRateLimiter(options => { ... })
    .UseRedis(config =>
    {
        config.EndPoints.Add("redis-master:6379");
        config.EndPoints.Add("redis-replica:6379");
        config.Password = "secret";
        config.Ssl = true;
        config.AbortOnConnectFail = false;
        config.ConnectTimeout = 5000;
        config.SyncTimeout = 3000;
    }, db: 0);
```

#### Способ 3: существующий IConnectionMultiplexer

Если подключение к Redis уже зарегистровано в DI другим пакетом:

```csharp
var multiplexer = ConnectionMultiplexer.Connect("localhost:6379");

builder.Services.AddRateLimiter(options => { ... })
    .UseRedis(multiplexer);
```

Или если `IConnectionMultiplexer` уже в DI:

```csharp
// RateLimiter найдёт существующий IConnectionMultiplexer автоматически
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("localhost:6379"));

builder.Services.AddRateLimiter(options => { ... })
    .UseRedis("localhost:6379");  // не создаст дубликат
```

#### Параметры Redis-хранилища

| Параметр | Значение по умолчанию | Описание |
|----------|----------------------|----------|
| `db` | 0 | Номер Redis БД |
| `stateTtl` | 10 минут | TTL записи в Redis |
| `MaxRetries` | 10 | Максимум попыток optimistic locking |
| `KeyPrefix` | `ratelimiter:` | Префикс всех ключей |

#### Что хранится в Redis

Ключ: `ratelimiter:{policyName}:{clientKey}`

Значение — JSON:

```
// Token Bucket
{"Tokens":73,"LastRefill":"2026-09-09T10:30:15.123Z"}

// Sliding Window
{"Requests":["2026-09-09T10:29:00Z","2026-09-09T10:29:15Z"]}
```

#### Какие операции пишут, какие читают

| Операция | Redis | Влияние на TTL |
|----------|-------|---------------|
| `TryConsumeAsync` | WATCH → GET → modify → MULTI → SET → EXEC | Обновляет TTL |
| `GetRemainingAsync` | GET (read-only) | Не трогает TTL |
| `GetLimitAsync` | Из фабрики (не лезет в Redis) | Не трогает TTL |
| `GetResetAsync` | GET (read-only) | Не трогает TTL |

---

## Идентификация клиента

По умолчанию — по IP-адресу через `IpKeyProvider`:

1. Проверяет заголовок `X-Forwarded-For` (первый адрес из цепочки)
2. Если нет — берёт `Connection.RemoteIpAddress`
3. Если и этого нет — `"anonymous"`

```csharp
builder.Services.AddRateLimiter(options => { ... })
    .UseKeyProvider<ClaimsKeyProvider>();
```

```csharp
public class ClaimsKeyProvider : IKeyProvider
{
    public string GetKey(HttpContext httpContext)
    {
        return httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? "anonymous";
    }
}
```

Ключ клиента + имя политики = уникальный ключ лимитера. Каждая пара (клиент, политика) имеет собственный лимит.

---

## Обработка 429

По умолчанию:

```
HTTP/1.1 429 Too Many Requests
Content-Type: application/json

{"message":"Rate limit exceeded."}
```

Плюс лог: `Rate limit exceeded. Path: /api/resource`

### Замена через DI

```csharp
builder.Services.AddRateLimiter(options => { ... })
    .UseRejectionHandler<ProblemDetailsHandler>();
```

```csharp
public class ProblemDetailsHandler : IRateLimitRejectionHandler
{
    public async Task HandleAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.5.29",
            title = "Too Many Requests",
            status = 429,
            retryAfter = context.Response.Headers["Retry-After"].FirstOrDefault()
        }));
    }
}
```

---

## HTTP-заголовки

Middleware добавляет заголовки при каждом запросе — и при успешном, и при отклонённом:

| Заголовок | Описание | Пример |
|-----------|----------|--------|
| `X-RateLimit-Limit` | Общий лимит | `100` |
| `X-RateLimit-Remaining` | Осталось | `73` |
| `X-RateLimit-Reset` | Unix-timestamp сброса | `1725880800` |
| `Retry-After` | Секунды до сброса (>= 1) | `42` |

---

## Атрибуты

### `[RateLimitPolicy("name")]`

Назначает политику эндпоинту или контроллеру. `AllowMultiple = false`.

```csharp
[RateLimitPolicy("strict")]
[HttpGet("search")]
public IActionResult Search() => Ok();
```

На классе — все методы контроллера используют эту политику.

### `[SkipRateLimiting]`

Полностью пропускает rate limiting. Заголовки не устанавливаются.

```csharp
[SkipRateLimiting]
[HttpGet("health")]
public IActionResult Health() => Ok();
```

---

## Расширение

### Свой алгоритм

**1. Ключ алгоритма:**

```csharp
public static class CustomAlgorithms
{
    public static readonly AlgorithmType LeakyBucket = new(nameof(LeakyBucket));
}
```

**2. Политика:**

```csharp
public class LeakyBucketPolicy : RateLimiterPolicy
{
    public override AlgorithmType Algorithm => CustomAlgorithms.LeakyBucket;

    public int BucketSize { get; set; }
    public TimeSpan LeakInterval { get; set; }
}
```

**3. Лимитер:**

```csharp
public class LeakyBucket : ISerializableRateLimiter
{
    public bool TryConsume(int tokens = 1) { ... }
    public int GetRemaining() { ... }
    public DateTime GetReset() { ... }
    public string Serialize() { ... }
    public void Deserialize(string state) { ... }
}
```

`Serialize`/`Deserialize` — только для Redis. Для InMemory достаточно `IRateLimiter`.

**4. Фабрика:**

```csharp
public class LeakyBucketFactory : IAlgorithmRateLimiterFactory
{
    public AlgorithmType Algorithm => CustomAlgorithms.LeakyBucket;

    public IRateLimiter Create(RateLimiterPolicy policy)
    {
        var lb = (LeakyBucketPolicy)policy;
        return new LeakyBucket(lb.BucketSize, lb.LeakInterval);
    }

    public int GetDefaultRemaining(RateLimiterPolicy policy)
    {
        return ((LeakyBucketPolicy)policy).BucketSize;
    }
}
```

**5. Extension-метод:**

```csharp
public static class LeakyBucketOptionsExtensions
{
    public static RateLimiterOptions AddLeakyBucket(
        this RateLimiterOptions options,
        string name,
        Action<LeakyBucketPolicy> configure)
    {
        var policy = new LeakyBucketPolicy { Name = name };
        configure(policy);

        if (policy.BucketSize <= 0)
            throw new ArgumentException("BucketSize must be > 0.", nameof(policy));
        if (policy.LeakInterval <= TimeSpan.Zero)
            throw new ArgumentException("LeakInterval must be > 0.", nameof(policy));

        options.Policies[name] = policy;
        return options;
    }
}
```

**6. Регистрация:**

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddLeakyBucket("leaky", lb =>
    {
        lb.BucketSize = 50;
        lb.LeakInterval = TimeSpan.FromSeconds(2);
    });
})
.UseAlgorithm<LeakyBucketFactory>();
```

---

### Своё хранилище

Реализовать `IDataStorage`:

```csharp
public interface IDataStorage
{
    ValueTask<bool> TryConsumeAsync(string key, int tokens = 1, CancellationToken ct = default);
    ValueTask<int> GetRemainingAsync(string key, CancellationToken ct = default);
    ValueTask<int> GetLimitAsync(string key, CancellationToken ct = default);
    ValueTask<DateTime> GetResetAsync(string key, CancellationToken ct = default);
}
```

| Метод | Назначение |
|-------|-----------|
| `TryConsumeAsync` | Потратить токен/запрос. Вернуть `true` если лимит не превышен |
| `GetRemainingAsync` | Сколько осталось |
| `GetLimitAsync` | Общий лимит (ёмкость) |
| `GetResetAsync` | Когда лимит сбросится |

Регистрация:

```csharp
builder.Services.AddRateLimiter(options => { ... })
    .UseStorage<DynamoDbStorage>();
```

---

### Свой обработчик 429

```csharp
builder.Services.AddRateLimiter(options => { ... })
    .UseRejectionHandler<ProblemDetailsHandler>();
```

---

### Свой идентификатор клиента

```csharp
builder.Services.AddRateLimiter(options => { ... })
    .UseKeyProvider<ApiKeyProvider>();
```

---

## Полный пример

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRateLimiter(options =>
{
    options.AddTokenBucketLimiter("api", tb =>
    {
        tb.TokenLimit = 100;
        tb.TokensPerPeriod = 10;
        tb.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
    });

    options.AddSlidingWindowLimiter("search", sw =>
    {
        sw.RequestsLimit = 30;
        sw.Window = TimeSpan.FromMinutes(1);
    });
})
.UseRedis(config =>
{
    config.EndPoints.Add("localhost:6379");
    config.AbortOnConnectFail = false;
}, db: 0)
.UseRejectionHandler<ProblemDetailsHandler>()
.UseKeyProvider<ClaimsKeyProvider>();

var app = builder.Build();

app.UseRateLimiter();
app.MapControllers();
app.Run();
```

```csharp
[RateLimitPolicy("api")]
[HttpGet("data")]
public IActionResult GetData() => Ok(data);

[RateLimitPolicy("search")]
[HttpGet("search")]
public IActionResult Search(string q) => Ok(results);

[SkipRateLimiting]
[HttpGet("health")]
public IActionResult Health() => Ok();
```

---

## Зависимости

| Пакет | Назначение |
|-------|-----------|
| `Microsoft.Extensions.Caching.Memory` | InMemory хранилище |
| `Microsoft.Extensions.Logging.Abstractions` | Логирование |
| `StackExchange.Redis` | Redis хранилище |

**Target Framework:** .NET 9.0

## Лицензия

MIT