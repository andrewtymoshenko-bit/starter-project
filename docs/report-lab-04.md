# Звіт до лабораторної роботи № 4

## Авторизація, ролі, володіння ресурсом та IDOR/BOLA

## 1. Ідентифікація роботи

- Дисципліна: «Прикладні технології програмування в інформаційній безпеці».
- Дата виконання: 19.10.2026.
- Предметний сценарій: спільний baseline 2-A, SecureLab; рівень «Добре».
- Гілка: `lab/4-authorization`; базовий тег: `v0.3.0`.
- Початковий commit ЛР 03: `69154171bcb12f018f06a3768441a230f18b703d`.
- Vulnerable commit hash: `fbb3d3f61f2972722d6b934125b9e9fa437d7cc9`.
- Фінальний тег для виправленого коду: `v0.4.0`.
- Репозиторій: https://github.com/andrewtymoshenko-bit/starter-project.
- Середовище: Windows, PowerShell, .NET 10, PostgreSQL у Docker; API — `http://localhost:5080`.

Позначка fixed hash замінюється фактичним hash після створення виправленого commit. Звіт із записаним hash зберігається наступним окремим commit; тег не пересувається.

## 2. Мета роботи

Відрізнити автентифікацію від авторизації, відтворити горизонтальний доступ до чужого Incident і виконання privileged action звичайним Reporter. Реалізувати перевірки ownership, role та поточного статусу; захистити дочірні коментарі й DTO; перевірити безпечний аудит відмови.

Очікуваний результат: заборонений запит не розкриває ресурс і не змінює БД, а власник і Analyst зберігають дозволені матрицею можливості.

## 3. Підготовка

Від власного `v0.3.0` створено гілку ЛР 04. Перевірено restore, build, PostgreSQL, reset і наявні тести. Seed-пароль передавався через `SeedUsers__Password` із прихованого вводу PowerShell; його значення не записувалося до сценаріїв чи звіту.

Перевірено login та `/api/me` Alice, Bob і Morgan; анонімний `/api/me` повертав 401. Ролі прочитано SQL-запитом із таблиць Identity, оскільки поточний `/api/me` не повертає роль.

| Користувач | Ідентифікатор | Роль |
|---|---|---|
| Alice | `10000000-0000-0000-0000-000000000001` | Reporter |
| Bob | `10000000-0000-0000-0000-000000000002` | Reporter |
| Morgan | `10000000-0000-0000-0000-000000000003` | Analyst |
| Admin | `10000000-0000-0000-0000-000000000004` | Administrator |

У `DbSeeder.cs` додано internal comment `30000000-0000-0000-0000-000000000003` до інциденту Alice, автор Morgan, `IsInternal = true`; public comments збережено. Після зміни seed виконано reset і тести. Захищений create з owner із principal успадковано з ЛР 03.

Окремий комплект resources викладачем не надано; сценарії та звіт підготовлено за PDF та адаптовано до власного проєкту.

## 4. Правила доступу

Повна восьмиколонкова матриця: access-matrix.md.

- Власник читає свій Incident; Analyst читає інциденти для аналізу.
- Текст редагує лише Reporter-власник; Analyst не має content update/delete.
- Delete заборонено всім: окремого дозволу baseline не визначає.
- Тільки Analyst виконує New → Triaged. Звичайний update не змінює severity/status.
- Internal comment доступний лише Analyst після parent authorization.
- Administrator не отримує автоматичного обходу ownership або прав Analyst.

Політика відмов: anonymous protected request — 401 без redirect; невідомий або невидимий ресурс — однакова 404 Problem Details; видимий ресурс, але заборонена дія — 403. Analyst triage не зі статусу New — 409 без зміни БД.

Обрана 404 зменшує розкриття існування чужого ресурсу. Спільний `IncidentNotFound()` не містить detail з identifier. Для чужого й невідомого ресурсу порівнюються status, Content-Type і весь JSON після вилучення traceId.

## 5. Finding 1: горизонтальний IDOR/BOLA

### Контекст і гіпотеза

Суб’єкт — Alice/Reporter; ціль — Incident Bob `20000000-0000-0000-0000-000000000002`. Гіпотеза: за незмінної cookie Alice підстановка identifier Bob дозволить читання або зміну чужих даних усупереч матриці.

### Стан до виправлення

`IncidentQueries.GetDetailsAsync()` відбирав ресурс тільки за id:

```csharp
.Where(incident => incident.Id == id)
```

Read-маршрут також ще не вимагав автентифікації; це окрема відсутність захисту входу до маршруту. Основний PoC виконано як автентифікована Alice.

Для mutation створено контрольований PUT із RequireAuthorization, але без ownership/resource decision. Entity знаходилася лише за id, після чого Title, Description і серверний UpdatedAtUtc зберігалися через SaveChangesAsync.

| Запит | Фактичний status до виправлення | Ефект |
|---|---|---|
| Alice → GET власного інциденту | 200 | Дозволені details Alice |
| Alice → GET Bob | 200 | Розкрито identifier, title та ownerDisplayName Bob |
| Alice → PUT Bob | 204 | Чужі title і description змінено |
| Alice → GET Bob після PUT | 200 | Прочитано збережений заголовок `LR04-IDOR-…` |

Mutation probe підтвердив п’ять значень true: title змінився; переданий title збережено; description змінився; status незмінний; severity незмінна.

Сценарій збережено в `tests/http/lab-04-mutation-probe.js`; передумова — login Alice на localhost. Він не містить пароля чи cookie value та призначений для vulnerable state. Після виправлення порівняння стану виконується автоматичними HTTP-тестами з окремим читанням БД: Alice більше не може читати Bob для такого порівняння.

Vulnerable commit: `fbb3d3f61f2972722d6b934125b9e9fa437d7cc9`.

### Першопричина

Identifier визначав target, але сервер не перевіряв право principal на action. Для GET дані одразу поверталися; для PUT виконувалося збереження без authorization decision. Наявність login або UUID не підтверджувала дозволу на конкретний ресурс.

### Виправлення

Додано `IncidentAuthorizationHandler` і операції `Read`, `UpdateContent`, `Delete`, `Triage`, `ViewInternalComment`. Handler зареєстровано як IAuthorizationHandler у Program.cs.

Група `/api/incidents` вимагає автентифікації. Details, update і delete завантажують entity, перевіряють Read, потім потрібну operation. Невідомий і невидимий ресурс повертають спільну 404; зміна entity можлива лише після дозволу. Для list owner scoping додається до SQL-запиту до проєкції DTO; Analyst може читати всі записи для аналізу.

User id і role-прапорець отримуються з principal в endpoint-шарі й передаються сервісу явно. OwnerUserId не додається до response DTO заради handler.

### Retest і позитивна регресія

Alice → Bob дає 404 для GET/PUT/DELETE. Негативні mutation-тести порівнюють owner, content, severity, status, timestamps і кількість дочірніх рядків до/після — змін немає. Власний GET Alice повертає 200, дозволений власний update — 204. Morgan читає Alice з 200, але update/delete як Analyst повертають 403 без зміни стану.

## 6. Finding 2: вертикальне виконання triage

### Стан до виправлення

Контрольований `POST /api/incidents/{id}/triage` вимагав входу, перевіряв New і серверно задавав Triaged, але не перевіряв Analyst role.

Alice створила власний тимчасовий інцидент: POST create — 201, status New. Далі triage як Reporter повернув 204, повторний GET — 200 зі статусом Triaged. Усі три перевірки probe дали true: createdAsNew, reporterWasAllowed, statusChangedToTriaged.

Причина — відсутня рольова перевірка privileged action. Серверне визначення next status саме по собі не захищало дію; для PoC не була потрібна підміна role у body.

### Виправлення й результат

Після Read endpoint викликає Triage operation; handler дозволяє її тільки Analyst. Потім endpoint перевіряє current status з БД. Для New сервер записує Triaged та history з verified actor id; для іншого статусу — 409. Роль перевіряється в handler, допустимість переходу — в endpoint; разом вони реалізують role + resource + context rule.

`ChangeIncidentStatusRequest` містить лише Note, довжина обмежена 1000 символами. Request не визначає owner, current/next status чи role.

Reporter triage власного New-інциденту отримує 403; snapshot БД та кількість history незмінні, навіть із forged role/status/isTransitionAllowed. Morgan отримує 204, історія містить actor Morgan і New → Triaged. Повторний transition повертає 409 без нових змін.

## 7. DTO й непрямий доступ

UpdateIncidentRequest приймає лише title/description. Обрано A-08a: невідомі JSON-поля ігноруються. Owner update із forged owner/role/severity/status/id/timestamps/boolean повертає 204 і змінює лише дозволені тексти й серверний UpdatedAtUtc; owner, рольові призначення та інший захищений стан незмінні.

`GET /api/incident-comments/{id}` перевіряє Read для parent Incident. Internal comment додатково потребує ViewInternalComment. Те саме рішення керує internal visibility у details-проєкції.

Alice читає public comment свого інциденту, але не comment Bob і не власний internal comment. Morgan читає дозволені public/internal comments. Admin не отримує автоматичного доступу до internal comment Alice.

## 8. Автоматизація та ізоляція тестів

Адаптовано або додано `IncidentEndpointTests`, `IncidentMutationAuthorizationTests`, `IncidentCommentAuthorizationTests`. Запити проходять через HTTP boundary WebApplicationFactory зі справжнім login і cookie, без demo-підміни principal. Різні суб’єкти мають окремі HttpClient; пароль читається зі змінної середовища.

Позитивні mutation-тести створюють власний fixture через POST і видаляють лише його та дочірні записи у finally. Негативні сценарії порівнюють БД до/після. Наявне assembly setting вимикає паралельність; reset не запускається з кожного тесту.

Окремі класи та повний suite успішно виконано в локальному середовищі. Точна кількість passed tests у тексті не фіксується; підтвердженням є фактичний підсумок запуску dotnet test.

## 9. Доказова таблиця

Наведено фактично виконані автоматичні й ручні перевірки. Назви методів посилаються на тести у репозиторії; власні скріншоти ручних результатів додаються до подання окремо.

| ID | Рівень | Сценарій | Передумови | Дія | Очікувано | Фактично | Доказ |
|---|---|---|---|---|---|---|---|
| A-01 | Добрий | Анонімне читання | Клієнт без cookie, redirects вимкнені | GET list/details/comment | 401 без redirect | 401, Location відсутній, application/problem+json | `ProtectedRead_AsAnonymous_Returns401WithoutRedirect` |
| A-02 | Достатній | Owner read | Login Alice, власний Incident | GET details Alice | 200, safe DTO | 200, очікуваний id/ownerDisplayName; ownerUserId/email відсутні | `GetDetails_AsOwner_ReturnsSafeDetails`; browser allow |
| A-03 | Достатній | Non-owner read | Alice, identifier Bob | GET details Bob | 404 без даних | 404; id/description/ownerDisplayName/comments не розкрито | `GetDetails_AsNonOwner_Returns404WithoutIncidentData`; PoC 200 до / 404 після |
| A-03R | Достатній, entry ЛР 05 | Analyst read | Login Morgan, Incident Alice | GET details Alice | 200 | 200, очікуваний id Alice | `GetDetails_AsAnalyst_CanReadAliceIncident` |
| A-04 | Добрий | Чужі mutations | Alice; snapshot Bob до запиту | PUT/DELETE Bob | 404, БД незмінна | 404/404; scalar state і кількості children збігаються | `Mutate_AsNonOwner_Returns404_AndPreservesState`; ручний retest |
| A-05 | Добрий | Scoped list | Alice; owner ids із БД | GET list | Лише власні | Власний seed є, Bob немає; усі ids належать Alice | `GetList_AsAlice_ReturnsOnlyOwnedIncidents` |
| A-06 | Добрий | Reporter privileged action | Власний New-fixture; forged role/status/boolean | POST triage | 403, state незмінний | 403, повний snapshot до/після однаковий | `Triage_AsReporter_Returns403_AndPreservesState`; ручний New → New |
| A-07 | Добрий | Analyst transition | Fixture Alice; окрема cookie Morgan | POST triage і повтор | 204 New → Triaged; повтор 409 | 204; actor/history правильні; повтор 409 без змін | `Triage_AsAnalyst_ChangesNewToTriaged_OnlyOnce` |
| A-08a | Добрий | Forged fields | Reporter-власник; update DTO | PUT із owner/role/status/severity/id/time | 204; лише content і updated time | 204; owner/roles/severity/status та захищений state незмінні | `Update_AsOwner_ChangesContent_ButIgnoresProtectedFields` |
| A-08b | Альтернатива | Відхилення зайвих полів | Обрано A-08a | Не застосовується | Лише один із A-08a/A-08b | Не застосовується — обрано A-08a | Контракт DTO та тест A-08a |
| A-09 | Добрий | Аудит відмови | Alice, власний New Incident 3 | POST triage, зіставлення log/response | Подія без secrets, однаковий traceId | 403, New → New; traceId збігся | Ручна подія API та Problem Details того самого запиту; розділ 10 |
| A-10 | Добрий, 404 | Однакові відповіді | Одна cookie Alice; foreign/random unknown ids | Парні GET details і child | Однакові status/Content-Type/JSON крім traceId | Обидві 404; DeepEquals після вилучення traceId | `GetDetails_ForeignAndUnknown_ReturnEquivalent404`; `GetComment_ForeignAndUnknown_ReturnEquivalent404` |
| A-11 | Відмінний | Search/summary scoping | Обрано «Добре» | Розширений сценарій не виконувався | Охоплення відмінного рівня | Не виконувалося — рівень «Відмінно» | Межі охоплення, розділ 12 |
| A-12 | Добрий | Child/internal visibility | Public Alice/Bob, internal Alice, різні ролі | GET comment/details | Parent access; internal лише Analyst | Alice own public 200, foreign/internal 404; Morgan internal/public 200; Admin internal Alice 404 | `GetComment_EnforcesParentAndInternalVisibility`; `GetDetails_ShowsInternalCommentOnlyToAnalyst` |

Додаткова регресія `Mutate_AsAnalyst_Returns403_AndPreservesState` підтверджує, що read-дозвіл Analyst не означає update/delete.

## 10. Browser retest та аудит

Після login Alice список містив лише дозволені інциденти; власні details відображалися. Виклик `loadIncidentDetails("20000000-0000-0000-0000-000000000002")` показав у блоці деталей «Помилка: Інцидент не знайдено».

У Client/app.js збережено createTextElement із textContent і detailsElement.textContent для помилки. Тест скрипту підтверджує відсутність innerHTML. Client відображає рішення сервера.

Фінальний ручний retest із Alice cookie:

```text
PUT    /api/incidents/20000000-0000-0000-0000-000000000002        -> 404
DELETE /api/incidents/20000000-0000-0000-0000-000000000002        -> 404
POST   /api/incidents/20000000-0000-0000-0000-000000000003/triage -> 403
Status before = New; status after = New
remainedNew = true; detailsUnchanged = true
```

Для останнього запиту спостерігалася structured event authorization.denied: subject Alice, action incident.triage, resource Incident 3, result denied, серверний UTC time і traceId. Використано `Activity.Current?.Id ?? httpContext.TraceIdentifier`. Фактичний traceId журналу точно збігся з traceId Problem Details; це підтверджено ручним порівнянням. Час та traceId тут не вигадуються: первинний доказ — власні записи відповіді й події.

До подання додаються власні скріншоти: ролі Identity; internal comment у seed; read/mutation IDOR до виправлення; Reporter triage до виправлення; підсумок suite; browser allow/deny; фінальний retest; аудит і відповідь з однаковим traceId. Ці локальні зображення не вбудовані автоматично у Markdown.

## 11. Відтворення

Команди виконуються з кореня проєкту. Для власного PostgreSQL-порту або connection string застосовуються ті самі локальні налаштування, що й раніше; їхні значення у звіті не наводяться.

Прихований ввід пароля в поточному PowerShell:

```powershell
$seedSecret = Read-Host 'Локальний навчальний пароль' -AsSecureString
$env:SeedUsers__Password = [Net.NetworkCredential]::new('', $seedSecret).Password
Remove-Variable seedSecret
```

Перед повним циклом зупиняють API; reset відновлює власні синтетичні дані:

```powershell
docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait
dotnet run --project src/SecureLab.Api --launch-profile http -- --reset-database
dotnet build --configuration Release
dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj --configuration Release
```

Після тестів для ручного retest:

```powershell
dotnet run --project src/SecureLab.Api --launch-profile http
```

Автоматичним HTTP-тестам окремий API не потрібний: його запускає фабрика, а PostgreSQL залишається локальною. Console використано для ручних PoC та аудиту; обов’язкові automated owner/non-owner та role/transition сценарії виконуються test harness через HTTP.

## 12. Залишкові ризики

1. **CSRF:** resource authorization не усуває CSRF cookie-authenticated mutations; це навчальний борг до ЛР 06.
2. **Search і summary:** A-11 не виконувався. Окремий search не отримує захист автоматично від іншої route group; severity-summary вимагає authentication, але не обмежує агрегацію owner. Ці шляхи можуть розкривати чужі записи або агрегати; основний perimeter не доводить повну ізоляцію всіх шляхів читання.
3. **Конкурентні переходи:** перевірка New перед SaveChanges не є optimistic concurrency. Послідовний повтор 409 не доводить захист від двох одночасних transitions. Окремий сценарій від викладача для цього не надавався.
4. **Свіжість ролі:** зміна ролі в Identity не оновлює миттєво вже видану cookie; потрібні relogin або окремий механізм оновлення.
5. **Межі 404:** порівнювалися status, Content-Type та стабільне JSON-тіло; timing side channels не досліджувалися.
6. **Обсяг:** перевірено названі HTTP-сценарії baseline. Не заявляється окремий тест кожної комбінації ролі та операції; нові batch/export не створювалися.

## 13. Git і подання

Fixed commit містить код, тести, фінальну матрицю та цю версію звіту; на нього встановлюється v0.4.0. Потім фактичний fixed hash записується у звіт окремим commit `Record lab 4 hashes`, без пересування тегу.

Передаються гілка lab/4-authorization, тег v0.4.0, hashes vulnerable/fixed states, цей звіт і фактичні докази. Перевірений стан інтегрується до погодженої основної гілки за прийнятим Git flow.

## 14. Використання ШІ

ChatGPT використовувався для аналізу, пояснення ownership/roles/HTTP, перевірки фрагментів коду, сценаріїв і тестів, аналізу помилки компіляції. Матеріал адаптовано до власних DTO, Identity, EF Core, seed identifiers і структури проєкту.

Я вносив зміни, запускав build/reset/тести, перевіряв браузерні сценарії та стан до/після, зіставляв traceId аудиту й відповіді та зберігав commits. Рішення перевіряв у власному середовищі;

## 15. Висновок

Відтворено читання й редагування інциденту Bob від імені Alice та Analyst-only triage звичайним Reporter. Першопричиною була відсутність ресурсного або рольового рішення перед поверненням чи зміною даних.

Реалізовано централізований handler, scoped list, захищені details/mutations, parent/internal visibility та DTO дозволених полів. Заборонені дії повертають узгоджені відповіді й не змінюють БД; owner read/update та дозволений Analyst transition працюють. Підтверджено однакові 404 й кореляцію аудиту з відповіддю.

Виконані перевірки відповідають заявленому рівню «Добре» в описаному perimeter. Залишкові ризики CSRF, додаткових шляхів читання та concurrent transitions зафіксовано окремо.
