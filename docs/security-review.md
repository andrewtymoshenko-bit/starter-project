# Підсумковий огляд безпеки SecureLab

Лабораторна робота №6. Посилення захищеності та підсумковий огляд безпеки.

Автор: Тимошенко Андрій, група КБ-41. Дисципліна: «Прикладні технології програмування в інформаційній безпеці». Рівень виконання: «Добре». Період перевірок: 08–09.11.2026.

## 1. Короткий підсумок

Перевірено власний локальний навчальний застосунок SecureLab після завершення ЛР5. Огляд охопив браузерний клієнт, ASP.NET Core API, cookie-сеанси, доступ до інцидентів, PostgreSQL, конфігурацію, NuGet-залежності та журнали. Під час роботи усунено дві підтверджені прогалини: зникнення захисних заголовків у відповіді на необроблену помилку та відсутність явної antiforgery-перевірки cookie-запитів зміни стану.

Заголовки перенесено в Response.OnStarting у middleware, яке охоплює exception-handling path. Додано стандартний ASP.NET Core Antiforgery, маршрут видачі request token після входу, спільний endpoint filter і браузерний helper. Фільтр підключено до створення й редагування інцидентів, DELETE, triage та logout. Додано окремий limiter пошуку з partition за автентифікованим user ID або IP анонімного клієнта; наявний limiter входу збережено. Успішний привілейований перехід доповнено структурованою audit-подією.

Локальні повторні перевірки підтвердили відмову без валідного CSRF-токена без зміни перевірених даних, успішні легітимні дії, незалежність search-квот Alice і Bob та відновлення після вікна. Повторено SQLi, XSS, CORS, authentication й authorization сценарії. У доступних джерелах NuGet на дату аудиту не знайдено відомих вразливих пакетів. У 164 наданих збігах пошуку не виявлено реальних hardcoded credentials; відкриті навчальні значення класифіковано окремо.

Це огляд локальної навчальної системи, а не сертифікація production deployment. Фінальна регресія на завершеному commit, прийнятий merge, анотований v1.0.0 та остаточний PDF ще не виконані на момент підготовки цього тексту.

## 2. Межі огляду та виключення

У межі входять власний accumulated project на baseline 2-A, HTML/JavaScript клієнт на localhost:5080, API, EF Core/Npgsql, локальна PostgreSQL у Docker Compose, Identity, конфігурація середовищ, прямі й транзитивні NuGet-пакети, відстежувані Git файли у явно заданих шляхах і журнали виконаних сценаріїв.

Тестові суб’єкти: Alice і Bob - Reporter, Morgan - Analyst, Admin - Administrator, анонімний клієнт та локальний розробник. Використовуються штучні навчальні дані. CORS probes працюють на localhost:4173 та localhost:4174.

Виключено сторонні системи, реальні користувачі, production hosting, мережевий perimeter, конфігурацію ОС і Docker загалом, аудит контейнерного образу на відомі CVE, реальні TLS-сертифікати, reverse proxy та HSTS production-host. Redis не використовується. Усі клони, stash, зовнішні CI та повна історія Git не перевірялися. Login-CSRF і небажане створення акаунтів не усуваються поточним post-login token flow та залишені явними залишковими ризиками.

## 3. Система та межі довіри

Браузер передає недовірені параметри та JSON до ASP.NET Core API і автоматично повертає cookie сеансу. API відновлює principal, перевіряє дозволи, DTO та antiforgery-пару й звертається через EF Core до PostgreSQL. Відповідь із текстами інцидентів і коментарів знову перетинає межу довіри перед відображенням у DOM. Конфігурація надходить із файлів і process environment; код і NuGet-пакети використовуються під час build/restore. Логи є окремим виходом даних із власними вимогами до мінімізації.

| Межа | Очікувана властивість | Конкретна перевірка |
|---|---|---|
| Браузер → API | Запит із cookie без валідного request token не змінює стан | REG-CSRF-01, REG-CSRF-02 |
| Principal → ресурс | Знання чужого ID не надає права читання або зміни | GetDetails_AsNonOwner_Returns404WithoutIncidentData; Mutate_AsNonOwner_Returns404_AndPreservesState |
| Роль → привілейована дія | Reporter не виконує triage; Analyst має лише визначені дозволи | Triage_AsReporter_Returns403_AndPreservesState; Triage_AsAnalyst_ChangesNewToTriaged_OnlyOnce |
| API → PostgreSQL | Пошуковий ввід не стає SQL-інструкцією | S-02; Search_SpecialCharacter_IsLiteral |
| Дані API → DOM | Description і Comment.Text не розбираються як HTML | REG-XSS-01, JSON/DOM comparison і відсутність дочірніх елементів |
| Інший origin → browser API read | Лише точний дозволений origin отримує browser-readable response | REG-CORS-01, origins 4173/4174 |
| Середовище → startup | Production справді активний і має інші конфігураційні умови | Hosting environment: Production; OpenAPI 404; CookieSecurityTests із перевіркою IHostEnvironment |
| Репозиторій і NuGet → build | Відомі advisory та можливі secrets оцінюються за фактичним контекстом | locked restore; NuGet audit; 164 класифіковані рядки |
| API → журнали | Події пояснюють дію без credentials і зайвого body | audit success/denied; пошук пароля, cookie/header та body marker |

## 4. Методика та середовище

Review-start commit: `52793f68d948bc5cd81081b20969a3bc57798bcc`. База: `v0.5.0`. Робоча гілка: `lab/6-hardening-review`. Попередні теги не переміщувалися в межах виконаних кроків.

| Компонент | Фактичне середовище |
|---|---|
| Команди | Windows, PowerShell |
| SDK | .NET SDK 10.0.302 |
| Target framework | net10.0 |
| API/client | http://localhost:5080 |
| БД | Локальний PostgreSQL через Docker Compose; порт хоста 54329, loopback binding |
| CORS probes | http://localhost:4173 та http://localhost:4174 |
| Production-like запуск | Release, --no-launch-profile, явне середовище Production та connection string поза Git |
| Cookie-тести | TestServer, https://localhost, окремо Development і Production |

Стартові locked restore, Release build і наявні тести виконано успішно, PostgreSQL був готовий до роботи. Reset/seed виконувався в Development до production-like перевірки. Для unexpected error лише process connection string спрямовано на недоступний локальний порт 54399; Compose не змінювався. Після експерименту відновлено Development, health, login і звичайний дозволений flow.

Фактичні результати взято з консольного виводу, браузерних таблиць, HTTP-відповідей, локальних логів, чорнетки та підтверджень виконання. Очікувані значення в процедурах повторення не видаються за результати майбутнього запуску.

## 5. Зведення знахідок

| ID | Дефект | Пріоритет | Підтвердження | Статус |
|---|---|---|---|---|
| F-01 | Захисні заголовки зникали на unexpected 500 | P3 | Порівняно фактичні 200/500 до виправлення, повторено після OnStarting | Fixed, локальний retest пройдено |
| F-02 | Cookie mutation не вимагала antiforgery request token | P2 | PUT без токена дав 204, повторний GET підтвердив зміну; після fix - 400 без зміни | Fixed у визначеному cookie-периметрі |

F-02 пріоритетніша через зв’язок із цілісністю бізнес-даних і керуванням сеансом. Експлуатованість із конкретного стороннього origin додатково залежить від browser policy, SameSite, CORS і формату запиту; повну cross-origin атаку не заявлено. Для F-01 самостійний exploit не демонструвався, а тіло перевіреної помилки вже було безпечним.

## 6. Підтвердження та виправлення

### F-01 - error path заголовків

До зміни POST /api/auth/login у Production з недоступною БД повернув 500 application/problem+json без CSP, X-Content-Type-Options і Referrer-Policy. Статичний GET / повертав 200 з цими заголовками. Причина: заголовки встановлювалися до endpoint, а exception handler очищував response. Middleware заголовків перенесено перед UseExceptionHandler, значення виставляються через Response.OnStarting.

Retest 08.11.2026 о 12:07:35 UTC дав 500 із `nosniff`, `no-referrer` і повною CSP, без stack trace в HTTP. Збережений traceId цієї відповіді: `00-3fb063db03d7cffa2723211399b17d9a-956198f3519fab3d-00`. Статична 200 також зберегла заголовки. Додатково перевірено protected API 200, validation 400 і resource 404.

### F-02 - antiforgery

До fix Alice виконала PUT власного інциденту без CSRF-header: HTTP 204, наступний GET 200 підтвердив новий заголовок. Установлено configuration gap; саме цей same-origin дослід не доводить повного стороннього exploit.

Сервіс AddAntiforgery використовує `X-CSRF-TOKEN`. GET /api/auth/csrf-token викликає GetAndStoreTokens і має RequireAuthorization. Cookie SecureLab.Antiforgery - HttpOnly, SameSite=Strict, Path=/, SecurePolicy=Always у Production та SameAsRequest у Development. AntiforgeryFilter викликає ValidateRequestAsync до handler і перетворює AntiforgeryValidationException на 400 Problem Details із title «Невалідний CSRF-токен».

На PUT без header та з `invalid-test-token` отримано 400 і незмінний JSON ресурсу; із валідною парою - 204, новий title збережено, description не змінено. Для create, PUT, DELETE, triage та logout без токена отримано 400 із характерним title, незмінними перевіреними даними й сеансом. Legitimate UI flow на двох сторінках, F5 та зміна користувача пройшли.

## 7. Регресійні перевірки та фактичні результати

| ID / сценарій | Спостережений результат | Спосіб |
|---|---|---|
| S-02, ЛР2 | Ввід `zz-no-match' OR TRUE -- `: 200, [] | curl, повтор первинного read-only сценарію |
| T-04, ЛР2 | USB: 200, відповідний seed-запис; O'Brien: 200, відповідний запис без 500 | curl з --data-urlencode |
| T-05, ЛР2 | sortBy=price: 400 Problem Details, errors.sortBy | curl |
| T-02, ЛР2 | Недопустимі DTO відхиляються; errors потрібного поля, відсутність створеного запису | Create_InvalidInput_Returns400 із валідним token |
| T-03, ЛР2 | Повтор нормалізованої назви дає 409 | Create_DuplicateTitle_Returns409 |
| Authentication, ЛР3 | Анонімний доступ відхилено; login/profile/logout і рівність auth-відмов перевірено | AuthenticationTests |
| Ownership, ЛР3 | Власник береться із сеансу Bob, підставлений ownerUserId не надає іншого власника | A04_A05_CreateAsBob_StoresBobAsOwner |
| Authorization/IDOR, ЛР4 | Чужий ресурс не читається/не змінюється; відмови 403/404 збережені | IncidentEndpointTests; IncidentMutationAuthorizationTests |
| A-06/A-07, ЛР4 | Reporter: 403 і незмінність; Analyst: 204 New → Triaged; повтор: 409 | Два triage-тести, 2/2 успішні; manual audit |
| REG-XSS-01 | Description відповідає JSON, 0 дочірніх елементів; comments текстові; marker null; Alice/Morgan | Browser DOM/JSON comparison |
| REG-CORS-01 | Origin 4173 дозволений; 4174 не дає JS читати; заборонений preflight не запускає GET | Два browser origins, Network і curl |
| REG-CSRF-01 | Missing/invalid: 400 і unchanged=true; valid: 204 і очікувана зміна | Authenticated browser, фактичний GET після кожного запиту |
| REG-CSRF-02 | П’ять маршрутів: 400, csrfProblem=true, stateAndSessionUnchanged=true | Browser із valid fixture та без token у перевірених запитах |
| REG-HEADERS-01 | CSP, nosniff, no-referrer на 200 HTML/API, 400, 404 та 500 після fix | Browser/curl; 500 - Production |
| REG-RATE-SEARCH-01 | Alice: 200,200,200,429; Bob:200; Alice після 11 с:200 | Окремі cookie-сеанси, тестові 3/10 |
| REG-RATE-LOGIN-01 | 401,401,429; після 4 с valid Alice:204; profile Alice | Тестові 2/3, PowerShell і наявні xUnit-тести |
| CookieSecurityTests | 2/2 успішні: cookie-прапорці та protected 400/404 у двох середовищах | HTTPS BaseAddress, environment assert |
| Dependency audit | Обидва проєкти: no vulnerable packages given current sources | dotnet package list --vulnerable --include-transitive |
| Secret search | 164/164 збіги класифіковано; real hardcoded secret у них не виявлено | Аналіз наданого git grep |
| Log review | П’ять точкових пошуків: 0; при ручному огляді чутливих даних не виявлено | Локальний Development log та окремий Production error evidence |

У проміжних збережених full-suite запусках: 51 тест, 51 успішний, 0 невдалих. Після розширення cookie-тесту окремо підтверджено 2/2 сценарії. Новий загальний total не підміняється розрахунком: його потрібно зафіксувати фактичним фінальним запуском.

Після введення PUT-фільтра три старі authorization-тести спочатку отримали 400 замість 204/403/404. Їм додано реальний token flow, початкові assertions і state checks збережено. До create/triage/logout-тестів також додано токен лише там, де потрібен автентифікований mutation. Загальний CookieClient.LoginAsync не робить token обов’язковим для всіх клієнтів, тому негативні CSRF-сценарії лишаються можливими.

## 8. Hardening controls

### Заголовки

Фактична CSP: `default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self'; frame-ancestors 'none'`. Вона обмежує джерела ресурсів і embedding; не замінює textContent, validation чи authorization. У Development для /scalar збережено виняток через UI документації. `X-Content-Type-Options: nosniff` зменшує ризик MIME sniffing, `Referrer-Policy: no-referrer` запобігає передаванню referrer. Наявність перевірено на п’яти класах відповідей.

### CORS

Policy Lab05BrowserProbe активна в Development: точний origin http://localhost:4173, GET, X-Lab-Cors-Probe, AllowCredentials. Origin 4174 не дозволено. Curl для /health повертає 200 для обох origins; Access-Control-Allow-Origin є лише для 4173. CORS обмежує browser-readable response і не є заміною authorization або antiforgery. Для cookie-запиту 4173 після Alice login спостерігався readable 200.

### Повний cookie-периметр

| ID | Method і route | Призначення | Auth-cookie | Механізм / рішення |
|---|---|---|---|---|
| SC-01 | POST /api/incidents | Створення | Так | Explicit AntiforgeryFilter |
| SC-02 | PUT /api/incidents/{id} | Редагування title/description | Так | Explicit AntiforgeryFilter |
| SC-03 | DELETE /api/incidents/{id} | Контракт DELETE і перевірка права | Так | Explicit AntiforgeryFilter; фізичне видалення не доведено цією CSRF-перевіркою |
| SC-04 | POST /api/incidents/{id}/triage | New → Triaged | Так | Explicit AntiforgeryFilter |
| SC-05 | POST /api/auth/logout | Завершення сеансу | Так | Explicit AntiforgeryFilter до SignOutAsync |
| SC-06 | POST /api/auth/login | Створення сеансу | Ні | Окремий login-CSRF ризик; поточний post-login token flow не застосовано |
| SC-07 | POST /api/auth/register | Створення акаунта | Ні | Окремий account-creation ризик; не оголошено antiforgery-protected |

Маршрути звірялися пошуком MapPost/MapPut/MapDelete та method у JS. Створення викликається auth-lab.html і browser-lab.html, logout - auth-lab.html. PUT, DELETE і triage існують як browser-accessible HTTP endpoints без окремих штатних форм у переглянутому UI; це задокументована різниця джерел, а не підстава виключити їх із захисту. GET/token routes не отримують mutation filter.

### Життєвий цикл токена

Helper csrf.js підключено перед page scripts на auth-lab.html та browser-lab.html. Токен отримується lazy перед mutation на поточній сторінці; після F5 пам’ять створюється заново. Після успішного login/logout кеш очищується, для іншого principal виконується нова видача. Cookie не копіюються в localStorage/sessionStorage. Helper дозволяє лише same-origin URL та не повторює POST автоматично після 4xx.

### Cookie і транспорт

Authentication-cookie - HttpOnly, SameSite=Lax; antiforgery-cookie - HttpOnly, SameSite=Strict, Path=/. У Production SecurePolicy.Always; у Development SameAsRequest. HTTPS test host підтвердив Secure, HttpOnly і потрібний SameSite для обох cookie. Це доказ application/cookie behavior, а не перевірка реального TLS.

Штатний launch profile містить лише http://localhost:5080. UseHttpsRedirection і UseHsts відсутні. HSTS на loopback довільно не додавалася. Production-like console запуск також використовував локальний HTTP лише для перевірки профілю та error handling. Захищені 400/404 у Production перевірені окремим cookie-aware test host. Reset у Production не виконувався; окремого фактичного доказу його відмови в поточному комплекті немає.

### Development і Production

| Властивість | Development | Production |
|---|---|---|
| Джерело connection string | Навчальна Development-конфігурація | Явне process environment / test setting |
| OpenAPI | Увімкнено | GET /openapi/v1.json: фактичний 404 |
| CORS probe policy | Увімкнена | Не реєструється |
| ApplyMigrationsOnStartup / AllowReset | true / true у навчальному profile | false / false за базовою конфігурацією |
| Cookie SecurePolicy | SameAsRequest | Always |
| Exception handler | Спільний safe Problem Details | Той самий safe mechanism, profile підтверджено окремо |

### Rate limiting

Search policy incident-search підключена лише до GET /api/incidents/search. Штатні fallback-параметри: PermitLimit=30, WindowSeconds=60, QueueLimit=0, fixed window, AutoReplenishment=true. Автентифікований partition - user:NameIdentifier; анонімний - ip:RemoteIpAddress. UseRateLimiter стоїть після authentication/authorization. Пошуковий ввід q та довільні client headers не використовуються як identity.

У досліді process overrides 3/10 дали 200,200,200,429 для Alice, 200 для Bob і 200 для Alice після 11 секунд. Login policy auth-login з IP partition збережена; тестові 2/3 дали 401,401,429, після 4 секунд правильний login Alice - 204. Тимчасові overrides прибрано. Рішення обмежене одним API instance.

## 9. Аудит залежностей і секретів

09.11.2026 виконано dotnet restore SecureLab.sln і dotnet package list --vulnerable --include-transitive зі SDK 10.0.302. Джерела: https://api.nuget.org/v3/index.json та локальний Microsoft SDK NuGetPackages. Для SecureLab.Api і SecureLab.Api.Tests виведено відсутність відомих вразливих пакетів у поточних джерелах. Diff csproj, packages.lock.json і Directory.Packages.props порожній. Оновлення, suppression чи вимкнення аудиту не застосовувалися. Це не аудит контейнерів і не доказ відсутності невідомих вразливостей.

Початковий широкий пошук поточного tracked state дав 169 рядків. Пізніший пошук `password|passwd|secret|api.?key|token|connectionstring` у src, tests, infra та README.md дав 164 рядки у 30 файлах; усі класифіковано. Більшість - CancellationToken, назви полів Identity, назви пакетів і runtime-обробка чутливих значень без hardcoded literals. Відкриті credentials локальної БД прийнято лише як study-only; штучний пароль негативного тесту - test-only. Реального credential у наданих рядках не виявлено. Різниця 169/164 пояснюється іншими шляхами пошуку та змінами коду; зменшення не є метрикою безпеки.

Перевірено, що infra/.env і src/SecureLab.Api/appsettings.Local.json ігноруються й не відстежуються. .env.example містить лише навчальне налаштування порту. Нові untracked helper/filter файли та повний актуальний docs state не охоплені автоматично наведеним grep; їх остаточний огляд перед commit ще потрібно завершити. Історія Git, stash, інші клони та external CI не включені. За справжнього витоку потрібні відкликання/ротація credential, а не лише видалення з останнього файла.

## 10. Журнали та аудит

Діагностичний журнал пояснює помилку, audit-подія реконструює значущу дію. Для unexpected error 08.11.2026 HTTP traceId `00-9034cf368cf13b5344b2773e52176658-29f0256922450603-00` зіставлено з ExceptionHandlerMiddleware: TraceId `9034cf368cf13b5344b2773e52176658`, SpanId `29f0256922450603`. HTTP містив safe title/status і не містив внутрішнього stack trace; stack trace лишався внутрішньою діагностикою.

Фактичний audit resource: `0a0d3fbc-8d67-4ca8-adcb-703d762e8941`.

| Subject | Action | Resource | Result | Час UTC |
|---|---|---|---|---|
| Alice, 10000000-0000-0000-0000-000000000001 | incident.triage | 0a0d3fbc-8d67-4ca8-adcb-703d762e8941 | denied, HTTP 403, стан New збережено | 2026-11-09T14:06:00.2623948+00:00 |
| Morgan, 10000000-0000-0000-0000-000000000003 | incident.triage | 0a0d3fbc-8d67-4ca8-adcb-703d762e8941 | success, HTTP 204, New → Triaged | 2026-11-09T14:08:59.7552217+00:00 |

Успіх логовано після SaveChangesAsync; історія переходу містить ChangedByUserId, old/new status і час. У структуровані audit-події не включено note або model dump. Відмову фіксує LogDeniedTriage.

Для локального Development-журналу перевірено successful/failed login, denied triage, validation error й permitted transition. Unexpected error має окремий Production-доказ. Пошук seed-пароля, `Cookie:`, `Set-Cookie:`, `X-CSRF-TOKEN:` та `LR06-LOG-BODY-MARKER-20261109` дав 0 рядків для кожного. Ручний огляд також не виявив чутливих даних. Нульові збіги не доводять відсутності всіх можливих витоків у будь-якому журналі.

## 11. Залишкові ризики та незавершені пункти

- Реальний deployment/TLS, certificate chain, reverse proxy й production HSTS не перевірялися.
- Login/register не покриті поточним post-login antiforgery flow.
- XSS у новому небезпечному sink здатна діяти від імені користувача попри antiforgery; CSP не замінює safe DOM.
- Fixed-window limiter допускає короткі bursts на межі вікон, state локальний для процесу, restart скидає квоти. Анонімні користувачі за NAT можуть ділити quota.
- AllowedHosts=* не забезпечує Host filtering для реального deployment.
- Secret review має явно визначені межі; повний final tracked state і нові файли треба остаточно оглянути перед commit.
- Фізичне видалення інциденту через DELETE не підтверджене позитивною регресією: у переглянутому вихідному handler після перевірок повертався 204 без виклику видалення. Підтверджені тут властивості DELETE - antiforgery denial та authorization, а не реалізація бізнес-видалення.
- Відмову reset у Production передбачає код, але окремого виконаного доказу зараз немає.
- Фінальний accepted commit, final full regression на ньому, merge, v1.0.0, доставка і остаточний PDF ще не завершені. Після їх виконання слід зафіксувати справжні значення, а не вигадувати hash або total.

## 12. Висновок

У визначеному локальному периметрі усунено підтверджені прогалини error-path headers і antiforgery. Перевірки містять і негативні запити, і читання фактичного стану, і легітимний flow. Authorization продовжує діяти після проходження CSRF-перевірки, а rate limiting не надає прав доступу. Пошуковий ввід лишається даними SQL-запиту, Description і Comment.Text відображаються текстом. Логи дають змогу зіставити безпечну помилку з діагностикою та реконструювати привілейований перехід без credentials.

Поточні результати підтримують виконання технічної частини рівня «Добре» у перевірених сценаріях. Завершення подання залежить від фінальної регресії на зафіксованому стані, оформлення Git і перевірки кінцевого звіту.

## 13. Доказові ідентифікатори

| Об’єкт | Фактичне значення |
|---|---|
| Review-start | 52793f68d948bc5cd81081b20969a3bc57798bcc |
| XSS Description fixture | 07caeca4-957a-4a58-ab3b-db152c7291c7 |
| XSS comment parent | 20000000-0000-0000-0000-000000000001 |
| XSS marker comment | 30000000-0000-0000-0000-000000000005 |
| Audit fixture | 0a0d3fbc-8d67-4ca8-adcb-703d762e8941 |
| Первинний safe 500 TraceId | 9034cf368cf13b5344b2773e52176658 |
| Первинний safe 500 SpanId | 29f0256922450603 |

У Markdown не перенесено значень auth-cookie або antiforgery request token зі скріншотів. У кінцевому звіті такі значення мають бути приховані; самі ідентифікатори запитів і навчальних ресурсів не є credential.