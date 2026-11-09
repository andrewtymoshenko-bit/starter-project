# F-02 Cookie mutation не вимагала antiforgery request token

Статус: Fixed у визначеному cookie-периметрі. Пріоритет: P2. Актив: цілісність інцидентів і керування сеансом. Компонент: JSON mutation endpoints та logout. Автор: Тимошенко Андрій, КБ-41. Перевірки: REG-CSRF-01 і REG-CSRF-02. Період:08–09.11.2026.

## Передумови та scope

Власний локальний SecureLab, Development, localhost:5080, навчальна БД і автентифікована Alice. Початок огляду: commit52793f68d948bc5cd81081b20969a3bc57798bcc, v0.5.0. Для baseline PUT використовувався власний seed-інцидент20000000-0000-0000-0000-000000000001.

Cookie-периметр: POST/api/incidents, PUT/api/incidents/{id}, DELETE/api/incidents/{id}, POST/api/incidents/{id}/triage, POST/api/auth/logout. Login/register не довіряють уже наявній auth-cookie та мають окреме рішення щодо ризику; вони не оголошені захищеними цим post-login flow.

## Відтворення до виправлення

1. Увійти Alice через штатну auth-сторінку.
2. GET власного інциденту має повернути200; зберегти початкові title і description.
3. Із Console власної сторінки надіслати PUT title/description з credentials:same-origin і Content-Type:application/json, без antiforgery-header.
4. Повторити GET і порівняти з початковим станом.

Фактичний результат: PUT204, GET200, title змінився на тестовий рядок. Очікувана цільова властивість: mutation із cookie без валідного request token відхиляється до handler, ресурс не змінюється.

## Вплив і межі твердження

Статус: локально спостережено прийняття mutation без token і підтверджено відсутність валідації в коді. Це дозволяло серверу покладатися лише на cookie/session та інші controls, не перевіряючи antiforgery-пару. Наслідки потенційно стосуються змін даних і сеансу. Повний exploit зі стороннього origin не демонструвався: його можливість залежить від SameSite, CORS, формату запиту й браузера. Сам факт успішного same-origin PUT не подано як доказ обходу всіх browser controls.

## Першопричина

JSON endpoints і DELETE не мали явного ValidateRequestAsync. Authentication установлювала principal, authorization перевіряла права, але жоден із цих механізмів не перевіряв antiforgery request token. CORS і SameSite не підміняють потрібну властивість цього cookie-profile.

## Виправлення

Зареєстровано AddAntiforgery: HeaderName=X-CSRF-TOKEN, cookie SecureLab.Antiforgery, HttpOnly, SameSite=Strict, Path=/. SecurePolicy - Always у Production, SameAsRequest у Development. Protected GET/api/auth/csrf-token викликає GetAndStoreTokens та повертає JSON token із no-cache/no-store.

Reusable AntiforgeryFilter викликає ValidateRequestAsync; лише AntiforgeryValidationException перетворює на400 Problem Details із title «Невалідний CSRF-токен». До next handler доходять лише запити після успішної перевірки. Фільтр явно приєднано до всіх п’яти cookie-mutation routes, а не до групи з GET. DisableAntiforgery не використано для обходу тестів.

Спільний csrf.js підключено перед page scripts на auth-lab.html і browser-lab.html. Він lazy отримує token, надсилає header тільки same-origin, зберігає token у пам’яті сторінки, очищує його після login/logout та401 і не повторює mutation автоматично після4xx. Після F5 token отримується заново. Cookie не копіюється в JS storage.

## Retest REG-CSRF-01

| Запит Alice до власного PUT | HTTP | Стан після наступного GET |
|---|---|---|
| Без request token | 400 | Незмінний, stateUnchanged=true |
| X-CSRF-TOKEN: invalid-test-token | 400 | Незмінний, stateUnchanged=true |
| Валідна пара cookie/request token | 204 | Новий title збережено; descriptionPreserved=true |

Відмова містить характерний CSRF-title, а не просто будь-який400. Порівнювався фактичний JSON до й після. Auth-cookie була присутня, і попередній дозволений GET проходив.

## Retest REG-CSRF-02

Створено окремий fixture з валідним token; далі для create, PUT, DELETE, triage й logout навмисно використано звичайний fetch без token-header. Усі п’ять дали400, csrfProblem=true та stateAndSessionUnchanged=true. Snapshot охопив details, list і /api/me. Цей тест доводить відмову й незмінність перевірених даних, а не повний аудит усіх таблиць. Для DELETE він не доводить реалізацію позитивного фізичного видалення.

## Позитивна регресія

Створення через auth-lab.html і browser-lab.html дало201; перехід між сторінками та F5 не зламали token flow. Logout дав204 з header, після нового login Bob одержав власний token і створив інцидент. У тестах authentication/authorization/validation спочатку виконано login і видачу token тим самим CookieClient. Assertions204/403/404 та перевірки state/DTO errors збережено. Три первинні PUT failure після введення фільтра виправлено адаптацією передумов, а не послабленням assertions.

## Залишковий ризик

Login-CSRF/account creation не усунуто поточним post-login flow. XSS може отримувати token штатним шляхом і діяти як principal, тому textContent/CSP залишаються потрібними. Локальний HTTP не доводить production transport safety. Нові mutation routes у майбутньому потребують такого самого фільтра й regression. Final regression на accepted commit і тегv1.0.0 ще не виконані.
