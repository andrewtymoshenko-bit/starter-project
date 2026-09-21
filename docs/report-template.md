# Звіт до лабораторної роботи № 1

## 1. Ідентифікація стану

- Варіант: 2-A «Трекер інцидентів».
- Гілка: `lab/1-system`.
- Фінальний тег: `v0.1.0`.
- Commit hash: `a4210f443fbd83df0fd20a10ff3c5cb0c60d04b8`.

## 2. Змінений маршрут

Реалізовано підсумок кількості інцидентів за severity: `GET /api/incidents/severity-summary`.

```text
клік кнопки "Показати підсумок" у Client/index.html
  → loadSeveritySummary у Client/app.js
  → GET /api/incidents/severity-summary[?status=...]
  → IncidentEndpoints.GetSeveritySummaryAsync
  → IncidentQueries.GetSeveritySummaryAsync
  → SecureLabDbContext.Incidents (таблиця incidents)
  → PostgreSQL
  → IncidentSeveritySummaryResponse
  → JSON response
  → textContent у Client/app.js
```

Ключові файли: `IncidentEndpoints.cs` (endpoint + серверна валідація `status`),
`IncidentQueries.cs` (`GroupBy`/`Count()`/`AsNoTracking()`),
`IncidentResponses.cs` (`IncidentSeveritySummaryResponse`),
`Client/index.html` і `Client/app.js` (кнопка, контейнер, три стани UI).

Політика нульових груп: лише наявні групи (без доповнення відсутніх
рівнів). Порядок: лексикографічний, через явний `OrderBy(group.Key)`,
оскільки `Severity` зберігається як текст.

## 3. Виконані зміни

1. Додано `IncidentSeveritySummaryResponse` у `Presentation/Contracts/IncidentResponses.cs`.
2. Реалізовано `IncidentQueries.GetSeveritySummaryAsync` з підтримкою
   необов'язкового фільтра `status`, структурованим логуванням кількості груп.
3. Замінено baseline `501`-заглушку на робочий endpoint у
   `IncidentEndpoints.cs`, додано серверну allowlist-валідацію `status`
   (той самий підхід, що і в `GET /api/incidents`), яка повертає `400` на
   некоректне значення.
4. Додано кнопку, статус-індикатор і контейнер результату в
   `Client/index.html`.
5. Реалізовано `loadSeveritySummary` в `Client/app.js` із трьома станами:
   завантаження, порожній результат ("Даних немає."), безпечна фіксована
   помилка — без stack trace чи внутрішніх деталей. Вивід через
   `textContent`/`createElement`.
6. Оновлено `tests/http/incidents.http`: очікування для `severity-summary`
   змінено з `501` на `200`, додано сценарії для порожнього
   (`?status=Resolved`) і некоректного (`?status=Unknown`) параметра.
7. Оновлено `docs/architecture.md`: додано маршрут, межі довіри,
   пояснення політики нульових груп і порядку.

## 4. Перевірка

| ID | Передумови | Дія | Очікувано | Фактично | Доказ |
|---|---|---|---|---|---|
| T-01 | Seed відновлено | `GET /health` | `200`, `{"status":"ready"}` | `200`, `{"status":"ready"}` | `HTTP/1.1 200 OK``{"status":"ready"}` |
| T-02 | Seed відновлено | `GET /api/incidents/{aliceIncidentId}` | `200`, деталі інциденту | `деталі в звіті pdf` | `деталі в звіті pdf` |
| T-03 | Seed відновлено | `GET /api/incidents/severity-summary` | `200`, `[{"severity":"High","count":1},{"severity":"Low","count":1},{"severity":"Medium","count":1}]` | `деталі в звіті pdf` | `деталі в звіті pdf` |
| T-04 | Seed відновлено | `GET /api/incidents/99999999-9999-9999-9999-999999999999` | `404 Problem Details`, `traceId` | `деталі в звіті pdf` | `деталі в звіті pdf` |
| T-05 | Seed відновлено | `GET /api/incidents/severity-summary?status=Unknown` | `400 Validation Problem Details` | `деталі в звіті pdf` | `деталі в звіті pdf` |
| T-06 | Seed відновлено | `GET /api/incidents/severity-summary?status=Resolved` | `200`, `[]` | `деталі в звіті pdf` | `деталі в звіті pdf` |
| T-07 | Seed відновлено, браузер відкрито | Клік кнопки "Показати підсумок" | Network показує `GET /api/incidents/severity-summary`, `200`; DOM безпечно показує результат | `деталі в звіті pdf` | `деталі в звіті pdf` |
| T-08 | — | `dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj` | `4/4` тести зелені | `деталі в звіті pdf` | `деталі в звіті pdf` |


## 5. Висновок

Реалізовано наскрізне розширення `GET /api/incidents/severity-summary` на
доброму рівні: серверна валідація `status` за allowlist, окремий response
DTO, структуроване логування, три стани клієнта (завантаження/порожньо/
помилка), безпечний DOM-вивід через `textContent`. Задокументовано і
перевірено політику нульових груп і сталий порядок результату. Усі базові
автоматизовані тести і ручні HTTP-сценарії
пройдені успішно, seed/reset відтворює початковий стан, diff перед комітом
перевірено на відсутність секретів.