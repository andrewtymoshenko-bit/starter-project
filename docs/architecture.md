# Карта архітектури

Це початкова карта, доповнена власним трасуванням запиту під час ЛР 1
(варіант 2-A, тег v0.1.0).

## Компоненти

| Компонент | Розташування | Відповідальність |
|---|---|---|
| Browser client | `src/SecureLab.Api/Client/` | Надсилає HTTP-запити, безпечно показує відповідь через DOM API |
| Presentation | `Presentation/` | Описує endpoints, читає зовнішні параметри, формує HTTP-відповідь |
| Application | `Application/` | Виконує сценарій отримання списку, деталей інциденту та підсумку за severity |
| Data | `Data/` | Відображає C#-сутності на PostgreSQL через EF Core/Npgsql |
| PostgreSQL | `infra/compose.yaml` | Зберігає навчальні дані у локальному контейнері |

## Наскрізний маршрут: перегляд деталей інциденту (досліджений, готовий)

```text
клік по картці інциденту у Client/app.js (loadIncidentDetails)
  → GET /api/incidents/{id}
  → IncidentEndpoints.GetDetailsAsync
  → IncidentQueries.GetDetailsAsync
  → SecureLabDbContext.Incidents (таблиця incidents)
  → PostgreSQL
  → IncidentDetailsResponse
  → JSON response
  → renderIncidentDetails у Client/app.js
  → DOM (textContent / document.createTextNode)
```

## Наскрізний маршрут: підсумок за severity (реалізовано в ЛР 1)

```text
клік кнопки "Показати підсумок" у Client/index.html
  → loadSeveritySummary у Client/app.js
  → GET /api/incidents/severity-summary[?status=...]
  → IncidentEndpoints.GetSeveritySummaryAsync
  → IncidentQueries.GetSeveritySummaryAsync
  → SecureLabDbContext.Incidents (таблиця incidents)
  → PostgreSQL
  → IncidentSeveritySummaryResponse (список)
  → JSON response
  → textContent у Client/app.js (summaryListElement)
```

**Ключові файли зміни:** `Client/index.html` (кнопка + контейнер результату),
`Client/app.js` (`loadSeveritySummary`), `Presentation/Endpoints/IncidentEndpoints.cs`
(`GetSeveritySummaryAsync`), `Application/Incidents/IncidentQueries.cs`
(`GetSeveritySummaryAsync`), `Presentation/Contracts/IncidentResponses.cs`
(`IncidentSeveritySummaryResponse`), `Data/SecureLabDbContext.cs` (`DbSet<Incident> Incidents`).

**Політика нульових груп:** обрано політику "лише наявні групи" — `GroupBy`
повертає лише severity, присутні в таблиці. На baseline seed це дає три
елементи (`High`, `Low`, `Medium`, кожен з `count: 1`), без `Critical`.

**Порядок елементів:** `Severity` зберігається як текст через
`HasConversion<string>()`, тому обрано явний лексикографічний порядок
(`OrderBy(group => group.Key)`), який дає стабільний, задокументований
результат `High, Low, Medium` — а не порядок критичності.

**Валідація:** необов'язковий query parameter `status` перевіряється за
allowlist значень enum `IncidentStatus` (той самий механізм, що і в
`GET /api/incidents`); некоректне значення дає `400 Validation Problem Details`.

## Межі довіри

| Межа | Чому даним ще не можна довіряти | Де перевіряємо або обмежуємо |
|---|---|---|
| Користувач → Browser client | Користувач контролює введення (вибір статусу у формі, клік кнопки) | Клієнт лише формує зручний UI; реальної перевірки тут немає |
| Browser client → API | Клієнт і сам HTTP-запит можна змінити поза UI (наприклад, через DevTools Console чи інший HTTP-клієнт); `<select>` на фронтенді не є серверним контролем | Серверна валідація `status` через `Enum.TryParse` + `Enum.IsDefined` в `IncidentEndpoints.GetSeveritySummaryAsync`, повертає `400` на некоректне значення |
| API → PostgreSQL | Endpoint не повинен довільно виконувати запит без обмеження й типізації | `AsNoTracking()` + типізований LINQ-запит (`GroupBy`/`Where`) через EF Core, без ручного SQL-рядка |
| PostgreSQL → API → DOM | У БД може зберігатися раніше введений недорований текст (наприклад, `description` seed-інциденту містить `<script>`) | Response DTO проєктує лише дозволені поля (`Severity`, `Count` — жодних вільних текстових полів із можливим HTML); клієнт виводить через `textContent`, а не `innerHTML` |

## Конфігураційні входи

- `global.json` — версія .NET SDK;
- `src/SecureLab.Api/appsettings*.json` — режим міграцій і локальний connection string;
- `infra/compose.yaml` — версія PostgreSQL, порт і локальні навчальні облікові дані;
- змінна середовища `ConnectionStrings__SecureLab` — безпечний спосіб перевизначити connection string поза репозиторієм.

## Повернення до відомого стану

```bash
dotnet run --no-build --project src/SecureLab.Api -- --reset-database
```

Команда застосовує наявні migrations, очищує лише відомі навчальні таблиці
локальної БД `securelab` і повторно заповнює їх фіксованими seed-даними.
Працює лише в явно налаштованому Development environment; не видаляє саму
БД чи схему.