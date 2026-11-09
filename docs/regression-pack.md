# Відтворюваний regression pack SecureLab для рівня Добре

Автор: Тимошенко Андрій, КБ-41. Лабораторна робота №6. Підготовлено 09.11.2026.

## Статус і правила виконання

Цей документ містить процедури та критерії повторення перевірок на власному локальному стенді. Наведені нижче «очікування» є критеріями успіху, а не вигаданим output майбутнього final run. Під час підготовчих прогонів підтверджені SQLi/XSS/CORS/CSRF/headers/rate-limit, cookie й audit результати. Проміжний full suite мав51/51, окремий cookie suite після розширення -2/2. Фінальний повний прогін на accepted commit і v1.0.0 ще не виконано.

Стартовий commit:52793f68d948bc5cd81081b20969a3bc57798bcc, v0.5.0. Робоча гілка lab/6-hardening-review. Відтворення здійснюється на поточному виправленому коді, а не шляхом повернення всього проєкту до vulnerable state. Перед будь-яким reset зберегти потрібні докази: reset очищує навчальні дані. Не запускати одночасно два API на5080.

У доказах зберігаються status, безпечні поля response, фактичний стан і відредаговані headers. Не публікувати значення auth-cookie, antiforgery-cookie, request token, seed-пароль або повний connection string. Credentials у процедурах Production - лише відкриті documented study/test значення локального стенда. Будь-які сторонні hosts виключені.

## 1. Початкові перевірки

PowerShell, корінь проєкту з SecureLab.sln; Docker Desktop працює, API зупинений.

```powershell
git status
git rev-parse HEAD
git tag --list "v0.*"
dotnet --version
docker compose version
docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait
dotnet restore SecureLab.sln --locked-mode
dotnet build SecureLab.sln -c Release --no-restore
```

Очікування: потрібний локальний PostgreSQL healthy, SDK10.0.*, успішний restore/build без warnings/errors. Невдалий крок виправляється перед наступним; assertions і policy не послаблюються.

Seed-пароль задати поза Git у тому самому PowerShell, де запускатимуться тести:

```powershell
$securePassword = Read-Host "Чинний навчальний seed-пароль" -AsSecureString
$credential = New-Object System.Management.Automation.PSCredential("study", $securePassword)
$env:SeedUsers__Password = $credential.GetNetworkCredential().Password
Remove-Variable securePassword, credential

if ([string]::IsNullOrWhiteSpace($env:SeedUsers__Password)) {
    throw "SeedUsers__Password не задано."
}
```

Для повернення початкових навчальних даних, якщо це потрібно для прогону, у Development і при зупиненому API:

```powershell
dotnet run --project src/SecureLab.Api -- --reset-database
```

Після reset довільні fixture IDs із попереднього прогону не гарантовано існують; створити їх знову, а не підставляти неіснуючий Guid. Сам reset не є доказом проходження регресії.

## 2. Автоматизована частина

```powershell
dotnet test SecureLab.sln -c Release
```

Очікування:0 failed,0 skipped без навмисного виключення сценаріїв. Зафіксувати фактичні total/passed/failed/skipped і commit. Не приписувати final run проміжний total51 або обчислений total52.

Додаткові цільові команди для діагностики, а не заміна full suite:

```powershell
dotnet test SecureLab.sln -c Release --filter "FullyQualifiedName~Triage"
dotnet test SecureLab.sln -c Release --filter "FullyQualifiedName~CookieSecurityTests"
dotnet test SecureLab.sln -c Release --filter "FullyQualifiedName~LoginRateLimitTests|FullyQualifiedName~A07_UnknownUserAndWrongPassword_ReturnSameResult"
```

Критерії: authentication/ownership/role assertions зберігаються; validation-тести мають errors потрібного поля й state check. Для authenticated mutation отримувати token тим самим HttpClient; negative CSRF request навмисно лишати без нього. Не перетворювати всі очікувані403/404 на400.

## 3. Development і health

```powershell
dotnet run --project src/SecureLab.Api
```

У консолі перевірити фактичне Hosting environment: Development. З іншого вікна:

```powershell
curl.exe -i http://localhost:5080/health
```

Очікування:200, {"status":"ready"}. Після login через http://localhost:5080/auth-lab.html власний список/details читаються. Protected401 не є доказом потрібних400/404; спочатку підтвердити сеанс.

## 4. S-02 і T-04/T-05 із ЛР2

Виконувати на fixed state із звичайними search limits; не використовувати series, яка сама вичерпує quota.

```powershell
curl.exe -i --get "http://localhost:5080/api/incidents/search" --data-urlencode "q=zz-no-match' OR TRUE -- "
curl.exe -i --get "http://localhost:5080/api/incidents/search" --data-urlencode "q=USB"
curl.exe -i --get "http://localhost:5080/api/incidents/search" --data-urlencode "q=O'Brien"
curl.exe -i --get "http://localhost:5080/api/incidents/search" --data-urlencode "sortBy=price"
```

| ID | Критерій |
|---|---|
| S-02 |200 і [], без розширення вибірки |
| T-04 USB |200, записи з відповідним текстом; на seed є навчальний USB-запис |
| T-04 O'Brien |200, відповідний classroom report, без500 |
| T-05 |400 Problem Details, errors.sortBy; не429 |

Для негативного висновку недостатньо будь-якого200: порівнюється саме склад вибірки. Буквальну семантику %, _, зворотного слеша й апострофа додатково перевіряє Search_SpecialCharacter_IsLiteral. Регресія доводить властивість цього search flow, не всіх можливих SQL-запитів.

## 5. REG-HEADERS-01 на п’яти класах

Статична сторінка:

```powershell
curl.exe -sS -o NUL -D - http://localhost:5080/
```

Очікування200 text/html, nosniff, no-referrer і CSP. У сеансі Alice наlocalhost:5080 виконати в Console:
```javascript
async function checkHeaders() {
    const routes = [
        "/api/incidents/20000000-0000-0000-0000-000000000001",
        "/api/incidents?status=Unknown",
        "/api/incidents/99999999-9999-9999-9999-999999999999"
    ];
    const results = [];
    for (const route of routes) {
        const response = await fetch(route, {
            credentials: "same-origin"
        });
        results.push({
            route,
            status: response.status,
            contentType: response.headers.get("Content-Type"),
            csp: response.headers.get("Content-Security-Policy"),
            nosniff: response.headers.get("X-Content-Type-Options"),
            referrer: response.headers.get("Referrer-Policy")
        });
    }
    console.table(results);
}
checkHeaders();
```

Три API-рядки мають status200,400,404 та CSP/nosniff/no-referrer. Перший Guid - власний seed-інцидент Alice; якщо fixture відсутній, підставити фактичний власний ID. Окремо п’ятий клас500 перевіряється в Production через unavailable DB за процедурою нижче. Для кожного рядка записати profile, route, status і реальні headers; не переносити заголовки з200 до500 без запуску.

## 6. REG-CORS-01

API лишається Development; Alice увійшла наlocalhost:5080. У двох PowerShell-вікнах із кореня:

```powershell
py -3 -m http.server 4173 --bind 127.0.0.1 --directory demonstrations/cors-probe
```

```powershell
py -3 -m http.server 4174 --bind 127.0.0.1 --directory demonstrations/cors-probe
```

Відкрити http://localhost:4173/ і http://localhost:4174/ у тому самому браузерному контексті. Не замінювати адресу браузера на127.0.0.1: origin має збігатися буквально. На кожній сторінці очистити Network та виконати:
```javascript
async function checkCors() {
    const cases = [
        {
            name: "Simple GET",
            path: "/health",
            options: { credentials: "omit" }
        },
        {
            name: "GET with preflight",
            path: "/health",
            options: {
                credentials: "omit",
                headers: { "X-Lab-Cors-Probe": "lr06" }
            }
        },
        {
            name: "GET with auth cookie",
            path: "/api/incidents",
            options: { credentials: "include" }
        }
    ];
    for (const test of cases) {
        try {
            const response = await fetch(
                "http://localhost:5080" + test.path,
                test.options
            );
            console.log(test.name, {
                status: response.status,
                readable: true
            });
            await response.text();
        } catch {
            console.log(test.name, {
                readable: false,
                result: "Fetch blocked or network error"
            });
        }
    }
}
checkCors();
```

На4173: simple GET200 readable; custom-header GET після дозволеного OPTIONS200; cookie GET/api/incidents200 readable. На4174: simple/cookie response недоступні JS, а заборонений preflight не запускає actual custom-header GET. Причину підтвердити Console CORS error і Network, а не лише catch: network failure має іншу причину.

```powershell
curl.exe -i -H "Origin: http://localhost:4173" http://localhost:5080/health
curl.exe -i -H "Origin: http://localhost:4174" http://localhost:5080/health
```

Обидва curl-запити200, ACAO лише для4173. Curl не застосовує browser SOP. Для дозволеного cookie response перевірити ACAO exact origin і Allow-Credentials:true. Same-site порти не роблять origins однаковими. CORS не замінює authorization/CSRF. Після досліду probes можна зупинити.

## 7. Видача token та REG-CSRF-01

Увійти Alice наlocalhost:5080; csrf.js уже підключений до auth-lab.html і browser-lab.html. Першу видачу перевірити на auth-сторінці:
```javascript
async function checkTokenIssuance() {
    const response = await fetch("/api/auth/csrf-token", {
        credentials: "same-origin"
    });
    if (response.status !== 200) {
        console.log("Token endpoint status:", response.status);
        return;
    }
    const body = await response.json();
    console.table([{
        status: response.status,
        tokenPresent:
            typeof body.token === "string" && body.token.length > 0,
        cacheControl: response.headers.get("Cache-Control")
    }]);
}
checkTokenIssuance();
```

Критерії:200, tokenPresent=true, Cache-Control:no-cache/no-store; antiforgery-cookie за потреби встановлено, HttpOnly. Значення token/cookie не друкувати. Ця видача не доводить валідацію mutation сама по собі.

На власній сторінці localhost:5080, Alice session, запустити повний PUT scenario:
```javascript
async function checkCsrfProtection() {
    const path =
        "/api/incidents/20000000-0000-0000-0000-000000000001";
    async function readIncident() {
        const response = await fetch(path, {
            credentials: "same-origin"
        });
        if (response.status !== 200) {
            throw new Error("GET status: " + response.status);
        }
        return await response.json();
    }
    async function updateIncident(title, description, token) {
        const headers = { "Content-Type": "application/json" };
        if (token !== undefined) {
            headers["X-CSRF-TOKEN"] = token;
        }
        return await fetch(path, {
            method: "PUT",
            credentials: "same-origin",
            headers,
            body: JSON.stringify({ title, description })
        });
    }
    const before = await readIncident();
    const missing = await updateIncident(
        "Must not change: missing token",
        before.description
    );
    const afterMissing = await readIncident();
    const invalid = await updateIncident(
        "Must not change: invalid token",
        before.description,
        "invalid-test-token"
    );
    const afterInvalid = await readIncident();
    const missingUnchanged =
        JSON.stringify(before) === JSON.stringify(afterMissing);
    const invalidUnchanged =
        JSON.stringify(before) === JSON.stringify(afterInvalid);
    console.table([
        {
            scenario: "Missing token",
            status: missing.status,
            stateUnchanged: missingUnchanged
        },
        {
            scenario: "Invalid token",
            status: invalid.status,
            stateUnchanged: invalidUnchanged
        }
    ]);
    if (
        missing.status !== 400 || invalid.status !== 400 ||
        !missingUnchanged || !invalidUnchanged
    ) {
        throw new Error("Негативна перевірка не пройшла");
    }
    const tokenResponse = await fetch("/api/auth/csrf-token", {
        credentials: "same-origin"
    });
    if (tokenResponse.status !== 200) {
        throw new Error("Token status: " + tokenResponse.status);
    }
    const { token } = await tokenResponse.json();
    if (typeof token !== "string" || token.length === 0) {
        throw new Error("Request token відсутній");
    }
    const newTitle = "LR06 valid CSRF token " + Date.now();
    const accepted = await updateIncident(
        newTitle,
        before.description,
        token
    );
    const afterAccepted = await readIncident();
    console.table([{
        scenario: "Valid token",
        status: accepted.status,
        expectedTitleSaved: afterAccepted.title === newTitle,
        descriptionPreserved:
            afterAccepted.description === before.description
    }]);
}
checkCsrfProtection();
```

Missing/invalid:400, stateUnchanged=true; valid:204, expectedTitleSaved=true, descriptionPreserved=true. У відмові має бути CSRF-title «Невалідний CSRF-токен». Якщо негативна частина не пройшла, процедура зупиняється до positive mutation. Порівнюється JSON ресурсу, а не лише response code. При іншому fixture змінити path на фактичний власний ID.

## 8. REG-CSRF-02 увесь cookie-периметр

На auth-lab.html як Alice; helper створить окремий fixture валідним token, подальші перевірені запити навмисно використовують звичайний fetch:
```javascript
async function checkCsrfPerimeter() {
    const created = await secureLabCsrf.fetch("/api/incidents", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            title: "LR06 perimeter " + Date.now(),
            description: "Окремий інцидент для перевірки CSRF.",
            severity: "Low",
            occurredAtUtc: new Date(Date.now() - 60000).toISOString()
        })
    });
    if (created.status !== 201) {
        throw new Error("Створення fixture: " + created.status);
    }
    const { id } = await created.json();
    const path = "/api/incidents/" + id;
    async function readJson(url) {
        const response = await fetch(url, {
            credentials: "same-origin",
            cache: "no-store"
        });
        if (response.status !== 200) {
            throw new Error(url + ": " + response.status);
        }
        return await response.json();
    }
    async function snapshot() {
        return JSON.stringify({
            incident: await readJson(path),
            list: await readJson("/api/incidents/"),
            session: await readJson("/api/me")
        });
    }
    const cases = [
        {
            method: "POST",
            url: "/api/incidents",
            body: {
                title: "Must not be created " + Date.now(),
                description: "Цей запис не повинен бути створений.",
                severity: "Low",
                occurredAtUtc: new Date(Date.now() - 60000).toISOString()
            }
        },
        {
            method: "PUT",
            url: path,
            body: {
                title: "Must not change",
                description: "Ця зміна не повинна зберегтися."
            }
        },
        { method: "DELETE", url: path },
        {
            method: "POST",
            url: path + "/triage",
            body: { note: "Цей перехід не повинен виконатися." }
        },
        { method: "POST", url: "/api/auth/logout" }
    ];
    const results = [];
    for (const test of cases) {
        const before = await snapshot();
        const response = await fetch(test.url, {
            method: test.method,
            credentials: "same-origin",
            headers: { "Content-Type": "application/json" },
            body: test.body === undefined
                ? undefined
                : JSON.stringify(test.body)
        });
        const problem = await response.json().catch(() => null);
        const after = await snapshot();
        const result = {
            method: test.method,
            route: test.url,
            status: response.status,
            csrfProblem: problem?.title === "Невалідний CSRF-токен",
            stateAndSessionUnchanged: before === after
        };
        results.push(result);
        console.table(results);
        if (
            result.status !== 400 ||
            !result.csrfProblem ||
            !result.stateAndSessionUnchanged
        ) {
            throw new Error("Перевірка не пройшла: " + test.url);
        }
    }
}
checkCsrfPerimeter();
```

П’ять рядків: create, PUT, DELETE, triage, logout. Критерії:400, csrfProblem=true, stateAndSessionUnchanged=true. Snapshot містить details, list і/api/me. Це перевірка валідації до handler; перевірка triage тут як Reporter не підміняє окрему перевірку ролей із валідним token. Створений fixture лишається навчальним записом. Фізичне видалення через DELETE ця негативна процедура не доводить.

Позитивні UI-кроки: Alice login → create наauth-lab201 → перехід наbrowser-lab іcreate201 → F5 іcreate201 → logout204 → Bob login іcreate201. На кожній сторінці token отримується lazy; у mutation є X-CSRF-TOKEN. Після зміни principal кеш очищується. Додатковий test stale-token після іншого principal, якщо не виконаний, не оголошувати пройденим лише за цим списком.

## 9. REG-XSS-01

Увійти Alice, відкрити browser-lab.html з Disable cache. Створити інцидент з унікальним title та description:

```html
<img src=x onerror="document.querySelector('#lab05-marker-target').setAttribute('data-lab05-marker','executed')">
Перевірка: апостроф ' і лапки ", 5 < 7, Україна 🇺🇦.
<b>Це має залишитися текстом</b>
```

Очікувати201→200 та буквальний текст, безimg/b елементів. Записати новий Guid. У виконаному09.11.2026 прогоні Guid був07caeca4-957a-4a58-ab3b-db152c7291c7; після reset створити новий.
```javascript
async function checkXssDom() {
    const id = document.querySelector("#incident-id").value.trim();
    const response = await fetch(
        "/api/incidents/" + encodeURIComponent(id)
    );
    if (response.status !== 200) {
        throw new Error("GET status: " + response.status);
    }
    const body = await response.json();
    const description = document.querySelector("#description");
    const comments = document.querySelector("#comments");
    const paragraphs = [...comments.querySelectorAll("p")];
    const apiComments = body.comments ?? [];
    console.table([{
        id,
        getStatus: response.status,
        descriptionMatchesJson:
            description.textContent === body.description,
        descriptionElementCount: description.children.length,
        commentsMatchJson:
            paragraphs.length === apiComments.length &&
            paragraphs.every((p, index) =>
                p.textContent === apiComments[index].text &&
                p.children.length === 0
            ),
        markerInCommentsJson:
            apiComments.some(comment =>
                comment.text.includes("data-lab05-marker")
            ),
        markerAttribute:
            document.querySelector("#lab05-marker-target")
                .getAttribute("data-lab05-marker")
    }]);
}
checkXssDom();
```

Для description: matchesJson=true, elementCount=0, markerAttribute=null. Для comment перевірити parent20000000-0000-0000-0000-000000000001: markerInCommentsJson=true, commentsMatchJson=true, markerAttribute=null. Marker comment має ID30000000-0000-0000-0000-000000000005. Якщо marker відсутній уJSON, comment-сценарій не пройдено: спочатку відновити fixture.

Прочитати обидва ресурси як Morgan та повторити DOM/JSON comparison. Одна лише відсутність виконання не доводить safe sink: CSP могла заблокувати inline-handler, тому перевіряються текст і відсутність HTML children. Для отримання comments потрібно спочатку натиснути «Прочитати» відповідного ресурсу, а не порівнювати JSON з DOM попередньої картки.

## 10. Search limiter два partitions

При зупиненому API в окремому process-window:

```powershell
$env:SearchRateLimit__PermitLimit = "3"
$env:SearchRateLimit__WindowSeconds = "10"
dotnet run --project src/SecureLab.Api
```

У test-window із заданим SeedUsers__Password:

```powershell
if ([string]::IsNullOrWhiteSpace($env:SeedUsers__Password)) {
    throw "Потрібний чинний SeedUsers__Password."
}

$aliceSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$bobSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession

foreach ($account in @(
    @{ Name = "alice"; Session = $aliceSession },
    @{ Name = "bob"; Session = $bobSession }
)) {
    $body = @{
        userName = $account.Name
        password = $env:SeedUsers__Password
    } | ConvertTo-Json -Compress

    $null = Invoke-WebRequest -UseBasicParsing `
        -Uri "http://localhost:5080/api/auth/login" `
        -Method Post -ContentType "application/json" `
        -Body $body -WebSession $account.Session

    $profile = Invoke-RestMethod `
        -Uri "http://localhost:5080/api/me" -WebSession $account.Session
    if ($profile.userName -ne $account.Name) {
        throw "Сеанс не належить очікуваному користувачу."
    }
}
Remove-Variable body

function Get-SearchStatus {
    param($Session)
    try {
        $response = Invoke-WebRequest -UseBasicParsing `
            -Uri "http://localhost:5080/api/incidents/search?q=LR06&sortBy=createdAtUtc" `
            -WebSession $Session
        return [int]$response.StatusCode
    }
    catch {
        if ($null -eq $_.Exception.Response) { throw }
        return [int]$_.Exception.Response.StatusCode
    }
}

$results = @()
foreach ($number in 1..4) {
    $results += [pscustomobject]@{
        Scenario = "Alice: запит $number"
        Status = Get-SearchStatus $aliceSession
    }
}
$results += [pscustomobject]@{
    Scenario = "Bob: власна квота"
    Status = Get-SearchStatus $bobSession
}
Start-Sleep -Seconds 11
$results += [pscustomobject]@{
    Scenario = "Alice: після вікна"
    Status = Get-SearchStatus $aliceSession
}
$results | Format-Table -AutoSize
```

Критерій:200,200,200,429,200,200. Перед серією не витрачати search quota іншими запитами. Якщо коротке вікно минуло під час налагодження, перезапустити лише дослід із відомими передумовами; не підмінювати невдалу ізоляцію очікуваним output.

Зупинити API й у його window прибрати лише власні temporary overrides:

```powershell
Remove-Item Env:SearchRateLimit__PermitLimit
Remove-Item Env:SearchRateLimit__WindowSeconds
dotnet run --project src/SecureLab.Api
```

Повертається штатний fallback30/60. Якщо до досліду у window вже були власні значення, відновити їх, а не просто видалити. In-memory limiter не є distributed control; state скидається після restart.

## 11. Login limiter і recovery

Зупинити API; в окремому window:

```powershell
$env:LoginRateLimit__PermitLimit = "2"
$env:LoginRateLimit__WindowSeconds = "3"
dotnet run --project src/SecureLab.Api
```

У test-window із чинним seed password:

```powershell
if ([string]::IsNullOrWhiteSpace($env:SeedUsers__Password)) {
    throw "Потрібний чинний SeedUsers__Password."
}
$loginSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession

function Send-LoginProbe {
    param([string]$UserName, [string]$Password)
    $body = @{ userName = $UserName; password = $Password } |
        ConvertTo-Json -Compress
    try {
        $response = Invoke-WebRequest -UseBasicParsing `
            -Uri "http://localhost:5080/api/auth/login" `
            -Method Post -ContentType "application/json" `
            -Body $body -WebSession $loginSession
        return [int]$response.StatusCode
    }
    catch {
        if ($null -eq $_.Exception.Response) { throw }
        return [int]$_.Exception.Response.StatusCode
    }
}

$results = @()
foreach ($number in 1..2) {
    $results += [pscustomobject]@{
        Scenario = "Невідомий користувач: $number"
        Status = Send-LoginProbe `
            -UserName ("missing-" + [guid]::NewGuid().ToString("N")) `
            -Password "Local-probe-only!123"
    }
}
$results += [pscustomobject]@{
    Scenario = "Alice після вичерпання quota"
    Status = Send-LoginProbe -UserName "alice" `
        -Password $env:SeedUsers__Password
}
Start-Sleep -Seconds 4
$results += [pscustomobject]@{
    Scenario = "Alice після вікна"
    Status = Send-LoginProbe -UserName "alice" `
        -Password $env:SeedUsers__Password
}
$results | Format-Table -AutoSize
$profile = Invoke-RestMethod -Uri "http://localhost:5080/api/me" `
    -WebSession $loginSession
"Поточний користувач: $($profile.userName)"
```

Критерій401,401,429,204 та profile alice. Анонімний/невдалий login не стає дозволеним через quota; тести auth-відмов і permissions залишаються окремими.

Зупинити API й прибрати overrides у його window:

```powershell
Remove-Item Env:LoginRateLimit__PermitLimit
Remove-Item Env:LoginRateLimit__WindowSeconds
```

## 12. Production-like profile та controlled500

Спочатку БД має бути підготовлена у Development. Окреме PowerShell-window, API зупинений:

```powershell
$db = "Host=127.0.0.1;Port=54329;Database=securelab;Username=securelab"
$db = "$db;Password=local-study-password;Include Error Detail=false"
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:DOTNET_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://localhost:5080"
$env:ConnectionStrings__SecureLab = $db
$env:Logging__Console__IncludeScopes = "true"
dotnet run --project src/SecureLab.Api --no-launch-profile -c Release
```

Перевірити фактичне Hosting environment:Production, listening5080. З іншого window:

```powershell
curl.exe -i http://localhost:5080/health
curl.exe -i http://localhost:5080/openapi/v1.json
```

Очікування200/ready та404 Problem Details без OpenAPI. Безпечна500 сама по собі не доводить, що profile активний.

Для error scenario зупинити API, змінити тільки process DB target та повторити startup у тому самому Production-window:

```powershell
$db = "Host=127.0.0.1;Port=54399;Database=securelab;Username=securelab"
$db = "$db;Password=unreachable-study-value;Include Error Detail=false"
$env:ConnectionStrings__SecureLab = $db
dotnet run --project src/SecureLab.Api --no-launch-profile -c Release
```

На54399 не має бути БД. У test-window:

```powershell
$body = @{ userName = "alice"; password = "NotARealPassword1!" } |
    ConvertTo-Json -Compress
$body | curl.exe -sS -i "http://localhost:5080/api/auth/login" `
    -H "Content-Type: application/json" -H "Accept: application/json" `
    --data-binary "@-"
Remove-Variable body
```

Критерій500 application/problem+json із safe title/status/traceId, без stack trace/SQL/connection string/local paths, із CSP/nosniff/no-referrer після F-01 fix.400/401/429 не підтверджують потрібного error path. У серверному ExceptionHandlerMiddleware зіставити32-символьний TraceId та16-символьний SpanId із сегментами W3C traceparent. У підготовчому доказі вони9034cf368cf13b5344b2773e52176658 і29f0256922450603; новий запуск має нові значення.

Після досліду зупинити API, закрити окреме Production-window й повернутися до звичайного Development-window з правильним DB target. Повторити health/login/дозволений GET. Не лишати несправний connection string активним.

Окрема запланована, ще не підтверджена перевірка: відмова --reset-database у Production з правильним connection string. Виконується тільки на власному стенді при зупиненому API через ту саму Production startup-команду з доданим `-- --reset-database`. Очікується негайна відмова InvalidOperationException до очищення даних. До фактичного виконання не позначати PASS.

## 13. Cookie і protected4xx

```powershell
dotnet test SecureLab.sln -c Release --filter "FullyQualifiedName~CookieSecurityTests"
```

Критерії:2/2 сценарії з явно перевіреним IHostEnvironment.EnvironmentName, окремо Development/Production. Auth-cookie HttpOnly/Secure/SameSite=Lax; antiforgery-cookie HttpOnly/Secure/SameSite=Strict/Path=/. Після login власний cookie-client одержує token200, validation400 Problem Details і resource404 Problem Details. Валідація auth-cookie до protected checks має працювати;401 не замінює ці результати.

TestServer із HTTPS BaseAddress не перевіряє сертифікат, шифрування мережі або HSTS. Штатний console profile HTTP, UseHttpsRedirection/UseHsts відсутні; цей факт оформляється як межа огляду, а не як вигаданий production TLS-доказ.

## 14. Audit і log review

Запустити справний Development API зі збереженням локального sink поза Git:

```powershell
New-Item -ItemType Directory -Force ..\lr06-logs | Out-Null
dotnet run --project src/SecureLab.Api 2>&1 |
    Tee-Object -FilePath ..\lr06-logs\api.log
```

У браузері наauth-lab.html виконати одну неправильну login-спробу, потім правильний Alice login. Для denied transition:
```javascript
async function checkReporterAudit() {
    const created = await secureLabCsrf.fetch("/api/incidents", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            title: "LR06 audit " + Date.now(),
            description: "Інцидент для перевірки журналу аудиту.",
            severity: "Low",
            occurredAtUtc: new Date(Date.now() - 60000).toISOString()
        })
    });
    if (created.status !== 201) {
        throw new Error("Створення: " + created.status);
    }
    const incident = await created.json();
    window.lr06AuditId = incident.id;
    const path = "/api/incidents/" + incident.id;
    const beforeResponse = await fetch(path);
    if (!beforeResponse.ok) throw new Error("Початковий GET");
    const before = await beforeResponse.json();
    const denied = await secureLabCsrf.fetch(path + "/triage", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ note: "Reporter audit probe" })
    });
    const afterResponse = await fetch(path);
    if (!afterResponse.ok) throw new Error("Повторний GET");
    const after = await afterResponse.json();
    console.table([{
        id: incident.id,
        status: denied.status,
        stateUnchanged: JSON.stringify(before) === JSON.stringify(after),
        incidentStatus: after.status
    }]);
}
checkReporterAudit();
```

Очікування403, stateUnchanged=true, incidentStatus=New. Записати отриманий fixture Guid. У консолі API - authorization.denied зі subject Alice, action incident.triage, resource Guid, result denied, часом і traceId.

Logout через UI, login Morgan на тій самій сторінці без F5. Якщо сторінку перезавантажено, вручну задати лише навчальний Guid у window.lr06AuditId. Далі:
```javascript
async function checkAnalystAudit() {
    if (!window.lr06AuditId) {
        throw new Error("Спочатку задай ID інциденту");
    }
    const path = "/api/incidents/" + window.lr06AuditId;
    const beforeResponse = await fetch(path);
    if (!beforeResponse.ok) throw new Error("Початковий GET");
    const before = await beforeResponse.json();
    const accepted = await secureLabCsrf.fetch(path + "/triage", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ note: "Analyst audit probe" })
    });
    const afterResponse = await fetch(path);
    if (!afterResponse.ok) throw new Error("Повторний GET");
    const after = await afterResponse.json();
    console.table([{
        id: window.lr06AuditId,
        status: accepted.status,
        before: before.status,
        after: after.status
    }]);
}
checkAnalystAudit();
```

Очікування204, New→Triaged, success audit після SaveChangesAsync. У підготовчому прогоні обидві дії стосувалися0a0d3fbc-8d67-4ca8-adcb-703d762e8941; Alice denied о14:06:00.2623948UTC, Morgan success о14:08:59.7552217UTC09.11.2026. При повторі використовуються фактичні нові IDs/час, а не ці старі значення як очікування.

Помилка DTO з валідним CSRF-token:
```javascript
async function checkValidationLogging() {
    const response = await secureLabCsrf.fetch("/api/incidents", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            title: "",
            description: "LR06-LOG-BODY-MARKER-20261109",
            severity: "Low",
            occurredAtUtc: new Date(Date.now() - 60000).toISOString()
        })
    });
    const problem = await response.json();
    console.table([{
        status: response.status,
        titleValidationError:
            Object.prototype.hasOwnProperty.call(
                problem.errors ?? {}, "title"
            )
    }]);
}
checkValidationLogging();
```

Очікування400, titleValidationError=true. Після виконання сценаріїв зупинити API, у window зі seed password перевірити лише числа:

```powershell
if ([string]::IsNullOrWhiteSpace($env:SeedUsers__Password)) {
    throw "SeedUsers__Password не задано."
}
$logPath = "..\lr06-logs\api.log"
if (-not (Test-Path $logPath)) { throw "Файл журналу не знайдено." }
$checks = @(
    @{ Name = "Seed password"; Value = $env:SeedUsers__Password },
    @{ Name = "Cookie header"; Value = "Cookie:" },
    @{ Name = "Set-Cookie header"; Value = "Set-Cookie:" },
    @{ Name = "CSRF header"; Value = "X-CSRF-TOKEN:" },
    @{ Name = "Request body marker"; Value = "LR06-LOG-BODY-MARKER-20261109" }
)
$checks | ForEach-Object {
    [pscustomobject]@{
        Check = $_.Name
        MatchingLines = @(
            Select-String -Path $logPath -SimpleMatch -Pattern $_.Value
        ).Count
    }
} | Format-Table -AutoSize
```

Критерій0 для кожного рядка. За ненульового числа дослідити локально й не виводити secret до доказу. Ручний review має додатково перевірити password/cookie/token/body/connection string у відповідних подіях. Нуль keyword matches не гарантує відсутності будь-якого витоку. Unexpected500 має окремий Production log/response evidence. Raw log не додавати до Git.

## 15. Dependencies і поточний secret scope

```powershell
dotnet --version
dotnet restore SecureLab.sln
dotnet package list --vulnerable --include-transitive
git diff -- '*.csproj' '**/packages.lock.json' 'Directory.Packages.props'
git check-ignore -v infra/.env src/SecureLab.Api/appsettings.Local.json
git ls-files 'infra/.env' '*appsettings.Local.json'
```

Критерій: audit фактично виконався, а не зупинився на network error; за advisory потрібне documented decision. Для підготовчого аудиту обидва проєкти не мали known vulnerable packages, diff порожній. .env/Local config ігноруються й не tracked. Жодного blind suppression або package update.

```powershell
$pattern = 'password|passwd|secret|api.?key|token|connectionstring'
$auditLines = @(git grep -n -I -i -E $pattern -- src tests infra README.md)
"Кількість рядків: $($auditLines.Count)"
$auditLines | ForEach-Object {
    $_ -replace '^([^:]+:\d+):.*$', '$1: [вміст приховано]'
}
git ls-files --others --exclude-standard
```

Повний текст збігів переглядати тільки локально й класифікувати за контекстом. Підготовчий164-row набір повністю класифіковано, але майбутній output може змінитися. Перевірити нові untracked files і документи окремо; після staging звірити final tracked state. Не оголошувати history/stash/CI перевіреними без окремої процедури.

## 16. Завершення прогону та Git

Після оформлення всіх текстів переглянути git status і diff, не відкидати чужі або власні зміни reset --hard. У staged state не має бути raw logs, .env, tokens/cookies, build artifacts або згенерованого PDF. Git delivery і merge виконуються за погодженим workflow проєкту, без force push і переміщення старих tags. Для захищеного main використовується merge request, а не прямий push.

На accepted commit виконати required full automated/manual regression із зафіксованими передумовами. Тільки після успіху створювати анотованийv1.0.0. Не створювати тег на ще не перевіреному іншому commit. Після tag звірити HEAD і git rev-list -n1v1.0.0; передати справжній hash і результати у кінцевий експорт. Майбутній hash не можна самопосилально записати в той самий commit.

### Поточний запис завершення

| Поле | Стан на момент підготовки |
|---|---|
| Review-start commit |52793f68d948bc5cd81081b20969a3bc57798bcc |
| Проміжні local checks | Виконано в описаних сценаріях |
| Accepted final commit | Ще не зафіксовано |
| Final full-suite total/result | Ще не отримано на accepted commit |
| Повтор manual pack на final commit | Ще не зафіксовано |
| Merge / v1.0.0 | Ще не завершено |
| Final PDF і приватна доставка | Ще не виконано |
