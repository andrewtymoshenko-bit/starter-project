# ЛР №5. Карта source-to-sink

Рівень: **«Добре»**. Description і Comment.Text є звичайним текстом; formatted HTML не дозволено. Шляхи в таблицях – від `src/SecureLab.Api/`.

## Incident.Description

Marker-інцидент для retest: `62afdb9e-661d-4fb6-8947-5ff015fa7728`.

| Крок | Місце в коді | Дані та перетворення |
|---|---|---|
| Source | `Client/browser-lab.html`, форма `#edit` | Користувацький текст у `textarea name="description"` |
| Request | `Client/browser-lab.js`, обробник `submit` | Значення поля → JSON `description` → `POST /api/incidents` |
| Validation / mapping | `Scaffolding/Lab02Endpoints.cs`, `CreateIncidentRequest.Description` | `Trim()`, required, максимальна довжина 4000; присвоєння `Incident.Description` |
| Storage | `Data/Entities/Incident.cs`, `Data/SecureLabDbContext.cs` | `Incident.Description` → PostgreSQL `incidents.description` |
| Response | `IncidentQueries.GetDetailsAsync`, `IncidentDetailsResponse.Description` | Після перевірки права читання GET повертає JSON `description` |
| Client transform | `Client/browser-lab.js`, `load()` | `response.json()` → `current.description` |
| Sink до fix | `load()`, `#description` | `innerHTML = current.description`: рядок розбирається як HTML |
| Sink після fix | `load()` → `renderPlainText` | `textContent` утворює текстовий вузол |

## IncidentComment.Text

Seed-інцидент: `20000000-0000-0000-0000-000000000001`. Public marker-коментар: `30000000-0000-0000-0000-000000000005`, автор Morgan, `IsInternal = false`.

| Крок | Місце в коді | Дані та перетворення |
|---|---|---|
| Source | `Data/DbSeeder.cs`, `SeedAsync` | Константа `Lab05Marker`; у продукті джерелом був би автор коментаря |
| Request | Відсутній для створення fixture | Коментар додається seed; GET лише читає інцидент |
| Mapping | `IncidentComments.AddRange`, `new IncidentComment` | `Text = Lab05Marker`, зв'язок із `aliceIncident` та `morgan` |
| Storage | `IncidentComment.Text`, `SecureLabDbContext` | PostgreSQL `incident_comments.text`, збереження через `SaveChangesAsync` |
| Response | `IncidentQueries.GetDetailsAsync`, `IncidentCommentResponse.Text` | JSON `comments[].text`; internal-коментарі фільтруються за правами |
| Client transform | `Client/browser-lab.js`, цикл у `load()` | Для кожного коментаря створюється `<p>` через `createElement` |
| Sink до fix | Той самий цикл | `paragraph.innerHTML = comment.text` |
| Sink після fix | Той самий цикл → `renderPlainText` | `paragraph.textContent`, додавання вузла до `#comments` |

## Спільне виправлення та інвентаризація

Обидва поля використовують helper у `Client/browser-lab.js`:

```javascript
function renderPlainText(element, text) {
  element.textContent = text ?? "";
}
```

| Місце | Походження / контекст | Рішення |
|---|---|---|
| Description і Comment.Text | Збережений недовірений текст у HTML sink | Обидва `innerHTML` замінено helper |
| Title та HTTP status у browser-lab | API title / сформований status | Уже використовують `textContent` |
| Поля форми | Користувацький текст / API data | `.value`, без HTML-парсера |
| Штатний `app.js`, `createTextElement` | API data у текстових вузлах | `createElement` і `textContent` |

Пошук небезпечних patterns у власних JS/HTML клієнта після fix не повернув збігів. Це результат інвентаризації, а не гарантія відсутності всіх можливих вразливостей.

**Висновок:** БД, validation та HTTP 200 не роблять текст довіреним HTML. Потоки мають ознаку stored XSS і клієнтський DOM sink. Після fix marker незмінний у JSON, але відображається текстом; CSP є додатковим захистом. Результати – у browser-security-evidence.md.
