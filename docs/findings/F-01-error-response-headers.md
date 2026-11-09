# F-01 Захисні заголовки відсутні у відповіді на необроблену помилку

Статус: Fixed, локально повторно перевірено. Пріоритет: P3. Компонент: middleware у src/SecureLab.Api/Program.cs. Актив: послідовність браузерної захисної політики HTTP-відповідей. Перевірка: REG-HEADERS-01. Автор: Тимошенко Андрій, КБ-41. Дата: 08.11.2026.

## Передумови

Власний SecureLab після v0.5.0; review-start commit 52793f68d948bc5cd81081b20969a3bc57798bcc. Локальний Production-like Release запуск через --no-launch-profile. API localhost:5080; connection string лише для цього процесу спрямований на недоступний PostgreSQL-порт54399. Cookie й реальний пароль для публічного login не потрібні. Перевірка проводиться тільки на власному стенді; новий debug endpoint не створюється.

## Відтворення до виправлення

1. Запустити API із ASPNETCORE_ENVIRONMENT=Production і DOTNET_ENVIRONMENT=Production, перевірити фактичний console profile.
2. У process ConnectionStrings__SecureLab указати недоступний локальний порт54399 та штучне значення пароля; Include Error Detail=false.
3. Виконати POST /api/auth/login з непорожніми вигаданими credentials та Accept: application/json.
4. Перевірити всю HTTP-відповідь, а не лише status.
5. Для позитивного порівняння виконати GET /, який віддає статичну сторінку незалежно від підключення БД.

Фактично 08.11.2026 о11:52:26UTC login повернув500 application/problem+json, але CSP, X-Content-Type-Options і Referrer-Policy були відсутні. Статичний GET/ повертав200 з усіма трьома заголовками. Очікування: налаштовані заголовки мають зберігатися й на error path, якщо profile не визначає для цього маршруту окремого винятку.

## Безпечний доказ

```json
{"title":"An error occurred while processing your request.","status":500,"traceId":"00-9034cf368cf13b5344b2773e52176658-29f0256922450603-00"}
```

У внутрішньому ExceptionHandlerMiddleware записі TraceId=9034cf368cf13b5344b2773e52176658, SpanId=29f0256922450603 та RequestPath=/api/auth/login. Тіло HTTP вже не розкривало stack trace; дефект цієї картки стосується заголовків.

## Вплив і ймовірність

Статус перевірки: локально спостережено. Response policy застосовувалася непослідовно: error response не мала налаштованих browser controls. Самостійний XSS/MIME/embedding exploit на цій JSON-відповіді не демонструвався; не заявлено витоку credential або виконання коду. ПріоритетP3 відповідає підтвердженій defense-in-depth прогалині без доведеного прямого впливу.

## Першопричина

Middleware установлювало headers до await next(). Під час exception стандартний handler очищував підготовлену response та формував Problem Details, унаслідок чого ранні headers втрачалися.

## Виправлення

Middleware заголовків поставлено перед UseExceptionHandler/UseStatusCodePages. Воно реєструє Response.OnStarting, який установлює заголовки перед фактичним надсиланням остаточної відповіді. Єдиний exception handler збережено; паралельний catch-all не додано. Development-виключення для /scalar лишилося явним.

Фінальна політика:

```text
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self'; frame-ancestors 'none'
```

## Retest і позитивна регресія

Той самий controlled login із недоступною БД після зміни08.11.2026 о12:07:35UTC дав500 із трьома заголовками, safe title/status і traceId00-3fb063db03d7cffa2723211399b17d9a-956198f3519fab3d-00. Статична сторінка лишилася200 і зберегла headers. Після відновлення справного Development перевірено власні details200, validation400 та resource404; headers присутні. Client list/details і login працюють. Конкретні значення заголовків узято з фактичних відповідей, не скопійовано між класами без перевірки.

## Залишковий ризик

Заголовки не замінюють safe DOM, validation, access control чи antiforgery. Відповіді зовнішнього reverse proxy не охоплено. Локальна перевірка не доводить TLS/HSTS production safety. Остаточний commit/tag і повтор на ньому ще не зафіксовані.
