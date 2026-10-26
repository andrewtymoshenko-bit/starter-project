# ЛР №5. Докази безпеки браузера

Рівень: **«Добре»**. API/client: `http://localhost:5080`; probes: `http://localhost:4173` і `http://localhost:4174`. Гілка: `lab/5-browser-xss`. Scaffold: `lab-05-start-v1`, база `v0.4.0` (`543c51517631112fc892899c689b54255d61f804`).

## Git-стани

| Стан | Commit |
|---|---|
| Vulnerable scaffold | `6ab523e8d12379410970b20f18e71852a8f8543f` |
| Marker-коментар до fix | `1f2dbf693393310836385b55b4a8d93104d6fe7d` |
| Safe rendering | `4509b756c4d6030ba368611baec8a783c975434e` |
| Development CORS policy | `7f10f66759846ef45da7deb28d1aec7eac595a1c` |

Підсумковий commit, `v0.5.0` і перенесення до `main` оформлюються після завершення документації.

## XSS: finding і результати

Першопричина: збережені Description і Comment.Text передавалися в `innerHTML`. У сеансі читача браузер створював HTML-елементи й виконував marker. Підтверджений вплив обмежений зміною контрольного DOM-атрибута. Fix – спільний `renderPlainText` із `textContent` для обох полів. Карта: source-to-sink.md.

Первинний marker-інцидент `192f8b91-8d09-486b-bc7f-59f3c1a1ead2` видалено reset під час додавання comment seed. До fix створено новий `62afdb9e-661d-4fb6-8947-5ff015fa7728`, використаний для retest. Marker-коментар `30000000-0000-0000-0000-000000000005` належить seed-інциденту `20000000-0000-0000-0000-000000000001`.

| Перевірка | До fix | Після fix |
|---|---|---|
| Marker у GET response | Збережений рядок | Той самий рядок |
| Description під Alice | `<img>`, marker `executed` | Текстовий вузол, marker `null` |
| Description під Morgan | GET 200, marker `executed` | GET 200, текст, marker `null` |
| Bob читає чужий запис | GET 404, marker `null` | Правила authorization збережено |
| Public marker-коментар | `<img>` у `#comments`, marker `executed` | Текст у `<p>`, marker `null` |
| Апостроф, лапки, `5 < 7`, Unicode | Текст зберігається | Показується повністю |
| `<b>жирний</b> текст` | HTML-елемент `<b>` | Буквальний текст без елемента `<b>` |
| 4000 літер `А` | POST 201 / GET 200, довжина 4000 | Той самий успішний результат |

Retest виконано до CSP й повторено у фінальному стані. Залишковий ризик: інші sinks та контексти потребують окремої перевірки; CSP не замінює safe rendering.

## Повторювані регресії

**REG-XSS-01.** Увійти Alice/Morgan в окремих сеансах, відкрити свіжу `/browser-lab.html` із Disable cache. Прочитати актуальний marker-інцидент, зіставити JSON `description` із DOM. Окремо прочитати seed-інцидент і перевірити marker у `comments[].text`. Очікування: GET 200, незмінний JSON, текстові вузли без `<img>`, контрольний атрибут відсутній. Позитивні рядки з таблиці також відображаються без змін. Для нового стенда після reset створити Description-запис із точним рядком `resources/lab05-marker.txt` і записати новий Guid. Негативний контроль – vulnerable commits, де marker виконувався.

Команда перевірки атрибута; після fix результат `null`:

```javascript
document.querySelector("#lab05-marker-target").getAttribute("data-lab05-marker")
```

**REG-CORS-01.** Запустити `demonstrations/cors-probe` через Python http.server на 4173 і 4174. У Console кожної сторінки виконати GET абсолютного `http://localhost:5080/health` без custom header та з `X-Lab-Cors-Probe: stage-3`. У Network порівняти Origin, OPTIONS, allow headers та actual GET. Cookie-дослід виконати кнопкою GET probe для `/api/incidents` після входу Alice на 5080; повторити в незалежному сеансі без входу. Direct HTTP перевірити curl із Origin 4173 і 4174; очікування наведені нижче.

## CORS policy та результати

`Lab05BrowserProbe` діє лише в Development: exact origin `http://localhost:4173`, GET, `X-Lab-Cors-Probe`, `AllowCredentials()`. Origin 4174 не дозволено. Порядок: routing → CORS → authentication → authorization → rate limiter.

| Сценарій | Результат |
|---|---|
| До policy: обидва origins | Simple/preflight запити не дають JS прочитати response |
| 4173, simple GET `/health` | JS читає 200/ready; точний Allow-Origin 4173 |
| 4173, custom header | OPTIONS 204 з allow origin/method/header, потім GET 200 |
| 4174, simple GET | Actual request доходить, response недоступний JS |
| 4174, custom header | OPTIONS без CORS-дозволів; actual GET не надсилається |
| 4173, cookie Alice, include | JS читає 200; Allow-Origin 4173, Allow-Credentials true |
| 4174, cookie Alice, include | Запит може виконатися, але response недоступний JS |
| 4173, include, без AllowCredentials | CORS-відмова; після повернення виклику – успіх |
| 4173, include, без входу | JS читає HTTP 401 із CORS headers |
| Curl, Origin 4173 / 4174 | Обидва `/health` дають 200; ACAO лише для 4173 |

Для direct HTTP використано `curl.exe -i -H "Origin: http://localhost:4173" "http://localhost:5080/health"` і той самий запит з Origin 4174. Curl читає body незалежно від ACAO, оскільки не застосовує browser SOP.

## CSP та захисні заголовки

У наявній middleware `Program.cs` застосовано enforcement:

```text
Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self'; frame-ancestors 'none'
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
```

`self` відповідає власним JS/CSS та API. Embedding не потрібне, тому `frame-ancestors 'none'`. `nosniff` обмежує небажану MIME-інтерпретацію; `no-referrer` запобігає передаванню адреси сторінки, яке цьому клієнту не потрібне. Заголовки перевірено на фактичному документі 200.

Вхід, list/details на `/` і `/browser-lab.html` працюють. Контроль `new Image().src = "http://localhost:4173/x.png"` заблокований CSP. Для `/scalar` є виняток лише в Development через сторонній UI довідника.

## Review і підсумок

XSS усунуто на DOM sink. CORS керує читанням response JavaScript, authorization – правом principal на ресурс. Cookie може надсилатися попри CORS-відмову; localhost-порти є cross-origin, але same-site. CSRF потребує окремого захисту запитів зміни стану в ЛР №6. CSP доповнює safe rendering.

Фінальні browser-перевірки, Release build і baseline tests успішні; `git diff --check` без зауважень. Скріншоти Network/Elements/Console та підсумок тестів додаються до звіту без паролів, cookie values і tokens.
