# Матриця доступу — лабораторна робота № 4

## 1. Контекст

- Предметний сценарій: baseline 2-A, SecureLab; рівень «Добре».
- Головний ресурс: `Incident`; дочірній: `IncidentComment`.
- Головна перевірювана дія: details/read; додатковий IDOR — content update.
- Privileged action: `POST /api/incidents/{id}/triage`.
- User id і ролі походять із verified principal. Owner і current status читаються з БД.

## 2. Політика відповідей

1. Анонімний protected request: `401` без redirect.
2. Невідомий або невидимий ресурс: спільний `IncidentNotFound()`, `404` Problem Details без detail з identifier.
3. Чужий і невідомий ресурс мають однакові status, Content-Type та JSON, крім traceId.
4. Видимий ресурс, але заборонена operation: `403`; наприклад, Reporter triage власного інциденту.
5. Analyst triage не зі статусу New: `409` без зміни БД.
6. Зміна entity, додавання history та SaveChanges виконуються лише після дозволу.

Політика 404 зменшує розкриття існування чужого ресурсу; перевірка однакових відповідей не доводить однаковий час виконання.

## 3. Реалізована матриця

Для конкретного identifier невідомий ресурс завжди повертає 404. Успішне читання: 200; create: 201; update і triage: 204.

| Дія | Рівень | Анонім | Reporter | Analyst | Administrator | Власник має значення? | Реалізація і перевірка |
|---|---|---|---|---|---|---|---|
| List | Добрий | deny, 401 | allow, лише власні | allow, усі для аналізу | allow, лише власні | Так, крім Analyst | `IncidentQueries.GetListAsync`, owner WHERE до проєкції; `GetList_AsAlice_ReturnsOnlyOwnedIncidents` (A-05) |
| Details | Достатній | deny, 401 | власний — allow; чужий — deny, 404 | allow для аналізу | власний — allow; чужий — deny, 404 | Так, крім Analyst | Handler `Read`; `IncidentEndpointTests`: A-01, A-02, A-03, A-03R, A-10 |
| Create | Успадкований baseline | deny, 401 | allow для себе | allow для себе | allow для себе | Owner призначає сервер | Захищений POST з ЛР 03, owner із principal; `IncidentOwnershipTests` |
| Update title/description | Добрий | deny, 401 | власний — allow; чужий — deny, 404 | deny, 403 | власний — deny, 403; чужий — deny, 404 | Так | Спочатку `Read`, потім `UpdateContent`; `IncidentMutationAuthorizationTests` (A-04, A-08a) |
| Delete | Добрий | deny, 401 | власний — deny, 403; чужий — deny, 404 | deny, 403 | власний — deny, 403; чужий — deny, 404 | Так для видимості | Handler `Delete` завжди відмовляє; перевірено Alice → Bob 404 та Analyst → Bob 403, БД незмінна (A-04) |
| Triage New → Triaged | Добрий | deny, 401 | власний — deny, 403; чужий — deny, 404 | allow лише для New; інший статус — 409 | власний — deny, 403; чужий — deny, 404 | Для дозволу Analyst — ні | Handler `Read` і `Triage`, потім status check в endpoint; A-06, A-07, A-09 |
| Public comment за прямим id | Добрий | deny, 401 | власний parent — allow; чужий — deny, 404 | allow після parent authorization | власний parent — allow; чужий — deny, 404 | Так, крім Analyst | `GetCommentAsync`, `Read` для parent; A-12 |
| Internal comment | Добрий | deny, 401 | deny, 404 | allow після parent authorization | deny, 404 | Одного ownership недостатньо | `ViewInternalComment` після `Read`; однаково для details і child; A-12 |

Це правила реалізації; не кожна можлива комбінація ролі й операції має окремий тест. Фактично виконане HTTP-покриття наведено у звіті.

## 4. Межі DTO

`UpdateIncidentRequest` містить лише Title і Description. Обрано A-08a: невідомі поля JSON ігноруються. Дозволений owner update повертає 204 і змінює content та серверний UpdatedAtUtc. Тест перевіряє незмінність owner, severity, status, часу створення/події, призначень ролей і кількості дочірніх записів.

`ChangeIncidentStatusRequest` містить лише Note. Поточний статус читається з БД; next status, actor і службовий час визначає сервер. Forged role/status/isTransitionAllowed не дають Reporter дозволу.

## 5. Обґрунтування

Reporter редагує текст власного інциденту. Analyst має read для аналізу та лише New → Triaged; це не право content update/delete. Administrator не отримує автоматичного обходу ownership або прав Analyst.

Окремого явного дозволу на delete у використаному baseline не визначено, тому обрано deny всім ролям. У цьому API немає дозволеного сценарію фізичного видалення; маршрут перевіряє видимість і повертає відповідну відмову.

Reporter не бачить internal comment навіть у власному інциденті. Analyst спочатку проходить перевірку parent incident, потім internal visibility.

## 6. Аудит

Перевірена відмова Reporter на triage власного ресурсу створює structured event `authorization.denied`: subject, action, resource, result, server UTC time, traceId. Використано `Activity.Current?.Id ?? httpContext.TraceIdentifier`; збіг із Problem Details підтверджено вручну. Password, cookie, token і raw body не журналюються. Ранні 404 не описуються як окремо перевірені аудиторські події.
