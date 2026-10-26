# Власна карта source-to-sink

Поле: Incident.Description: Користувач O'Brien написав \"терміново\".. Стан/commit: заповніть. Вхід/Guid запису: 0813f57b-1705-4859-89bc-0b36317e07e4.

| Крок | Конкретне місце | Що відбувається |
|---|---|---|
| Source | `browser-lab.html`, форма `#edit`, `textarea name="description"` | Користувач вводить недовірений текст |
| Request | `browser-lab.js`, обробник `submit` | Значення передається в JSON-полі `description` через POST |
| Validation/mapping | `Scaffolding/Lab02Endpoints.cs`, POST handler, `CreateIncidentRequest.Description` | Виконується `Trim()`, перевіряються обов’язковість і максимальна довжина 4000; значення присвоюється `Incident.Description` |
| Storage | `Incident.Description`, PostgreSQL | `SaveChangesAsync` зберігає текст; він залишається недовіреним для HTML |
| Response | `IncidentQueries.cs`, `GetDetailsAsync`; DTO `IncidentDetailsResponse` | Опис повертається через GET у JSON-полі `description` |
| Client transform | `browser-lab.js`, `load()` | `response.json()` створює об’єкт `current`; опис доступний як `current.description` |
| Sink/context | `load()`, `#description.innerHTML` | Рядок передається HTML-парсеру, тому може створювати елементи DOM |

| Крок | Конкретне місце |
|---|---|
| Source | `DbSeeder.SeedAsync`, константа `Lab05Marker`; у продукті джерелом був би автор коментаря |
| Request | Відсутній для створення: тестові дані додаються seed; GET лише читає запис |
| Mapping | `new IncidentComment`, `Text = Lab05Marker` |
| Storage | `IncidentComment.Text`, збереження через `SaveChangesAsync` |
| Response | `IncidentQueries.GetDetailsAsync` → `IncidentCommentResponse` → `comments[].text` |
| Client transform | `browser-lab.js`, цикл `for` у `load()` |
| Sink/context | `paragraph.innerHTML = comment.text`, інтерпретація як HTML |


Інвентаризація: файл/функція → категорія даних → контекст → обґрунтоване рішення. Для «Добре / Відмінно» додайте повний другий потік IncidentComment.Text за тією самою таблицею. Для «Відмінно» додайте результат поглибленої перевірки comments/повторного render. Рівні накопичувальні; основний Description потрібний для всіх.
