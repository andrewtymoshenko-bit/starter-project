# Перевірка потенційних секретів

Дата перевірки: 09.11.2026.

## Джерела та межі

Таблицю заповнено за фактичним локальним виводом `$auditLines`. Зіставлено всі 164 рядки: для кожного є відповідний вихідний збіг. Розташування й номери рядків збережено; це номери на момент виконання пошуку, після редагування вони можуть зміститися.

Фактична команда пошуку:

```powershell
git grep -n -I -i -E 'password|passwd|secret|api.?key|token|connectionstring' -- src tests infra README.md
```

Пошук охоплював поточний вміст відстежуваних Git файлів у `src/`, `tests/`, `infra/` та `README.md`. Це 164 рядки зі збігами, а не 164 секрети. Збіги з попереднього ширшого пошуку (169) безпосередньо з цим числом не порівнюються: змінилися і код, і перелік шляхів.

У наданому консольному виводі українські фрагменти місцями відображені з помилкою кодування. Класифікація спирається на збережені ідентифікатори, виклики API, літерали та контекст відомого навчального проєкту; вихідні файли через це не змінювалися.

## Підсумок

- Класифіковано: **164 із 164** наданих збігів.
- Реальних hardcoded credentials або фактичних значень cookie/request token у наданих рядках не виявлено.
- Відкриті навчальні credentials БД та вигаданий пароль негативного тесту прийняті лише для локального навчального контексту; це не дозвіл на їх використання в інших середовищах.
- Рядки роботи з runtime-паролями й токенами не визнано нешкідливими для будь-якого використання: тут відсутні вбудовані значення, але ці дані залишаються чутливими під час виконання. Їх журналювання перевіряється окремим log review.
- Неперевірених рядків у цій таблиці немає. Це не означає повного аудиту всіх файлів і всіх потоків даних.

## Класифікація кожного збігу

| Розташування | Класифікація | Рішення та причина |
|---|---|---|
| README.md:63 | Документація навчальних credentials | accepted, study-only: опис відкритих credentials локальної БД; не переносити в інші середовища. |
| README.md:66 | Назва змінної конфігурації | ConnectionStrings__SecureLab - назва джерела налаштування; значення credential у цьому рядку відсутнє. |
| README.md:135 | Назва змінної конфігурації | ConnectionStrings__SecureLab - назва джерела налаштування; значення credential у цьому рядку відсутнє. |
| README.md:138 | Інструкція з безпеки | Перелік заборонених до публікації даних; фактичних паролів, cookie або токенів немає. |
| infra/compose.yaml:9 | Відкриті навчальні credentials БД | accepted, study-only: локальна навчальна БД; узгоджено з README та loopback-прив’язкою Compose. Не реальний production-секрет; повторне використання поза стендом заборонене. |
| src/SecureLab.Api/Application/Incidents/IncidentQueries.cs:16 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Application/Incidents/IncidentQueries.cs:46 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Application/Incidents/IncidentQueries.cs:52 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Application/Incidents/IncidentQueries.cs:78 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Application/Incidents/IncidentQueries.cs:84 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Application/Incidents/IncidentQueries.cs:100 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Client/auth-lab.html:43 | HTML-атрибут поля пароля | Не секрет: name/type/autocomplete описують поле вводу; готового значення пароля в наведеному рядку немає. |
| src/SecureLab.Api/Client/auth-lab.html:44 | HTML-атрибут поля пароля | Не секрет: name/type/autocomplete описують поле вводу; готового значення пароля в наведеному рядку немає. |
| src/SecureLab.Api/Client/auth-lab.html:45 | HTML-атрибут поля пароля | Не секрет: name/type/autocomplete описують поле вводу; готового значення пароля в наведеному рядку немає. |
| src/SecureLab.Api/Client/auth-lab.html:57 | HTML-атрибут поля пароля | Не секрет: name/type/autocomplete описують поле вводу; готового значення пароля в наведеному рядку немає. |
| src/SecureLab.Api/Client/auth-lab.js:80 | Очищення поля пароля | Не секрет: присвоєння порожнього рядка полю форми після операції; пароль не вбудовано в код. |
| src/SecureLab.Api/Client/auth-lab.js:193 | Очищення поля пароля | Не секрет: присвоєння порожнього рядка полю форми після операції; пароль не вбудовано в код. |
| src/SecureLab.Api/Data/DatabaseBootstrap.cs:35 | Метадані таблиці або типу Identity | Не секрет: назва таблиці, типу, ключа чи зовнішнього ключа; значення токенів у рядку відсутні. |
| src/SecureLab.Api/Data/DbSeeder.cs:39 | Читання пароля поза Git | У коді лише ім’я ключа SeedUsers:Password / SeedUsers__Password; фактичне значення надходить із конфігурації середовища. |
| src/SecureLab.Api/Data/DbSeeder.cs:42 | Назва поля password hash | Не містить hash: оголошення колонки/властивості або перевірка її відсутності; фактичний hash не задано. |
| src/SecureLab.Api/Data/DbSeeder.cs:45 | Діагностичне повідомлення про конфігурацію | Названо відсутній ключ або вимогу задати його поза Git; фактичний пароль у повідомленні не наведено. |
| src/SecureLab.Api/Data/DbSeeder.cs:49 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| src/SecureLab.Api/Data/DbSeeder.cs:51 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| src/SecureLab.Api/Data/DbSeeder.cs:53 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| src/SecureLab.Api/Data/DbSeeder.cs:55 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| src/SecureLab.Api/Data/DbSeeder.cs:184 | Параметр або поле пароля | Оголошення типу/параметра для runtime-вводу; значення credential у коді відсутнє. |
| src/SecureLab.Api/Data/DbSeeder.cs:189 | Перевірка наявності пароля Identity | Не секрет: перевіряється факт наявності пароля; значення пароля або hash не виводиться. |
| src/SecureLab.Api/Data/DbSeeder.cs:191 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| src/SecureLab.Api/Data/DbSeeder.cs:192 | Діагностика відсутнього seed-пароля | Не секрет: повідомлення про відсутнє налаштування або перевірка null; конкретного значення пароля немає. |
| src/SecureLab.Api/Data/DbSeeder.cs:210 | Діагностика відсутнього seed-пароля | Не секрет: повідомлення про відсутнє налаштування або перевірка null; конкретного значення пароля немає. |
| src/SecureLab.Api/Data/Migrations/20260902062130_Lab03Identity.Designer.cs:35 | Метадані конкурентного доступу EF Core | Не секрет: позначення властивості для контролю конкурентних змін, без значення credential. |
| src/SecureLab.Api/Data/Migrations/20260902062130_Lab03Identity.Designer.cs:139 | Метадані таблиці або типу Identity | Не секрет: назва таблиці, типу, ключа чи зовнішнього ключа; значення токенів у рядку відсутні. |
| src/SecureLab.Api/Data/Migrations/20260902062130_Lab03Identity.Designer.cs:155 | Метадані таблиці або типу Identity | Не секрет: назва таблиці, типу, ключа чи зовнішнього ключа; значення токенів у рядку відсутні. |
| src/SecureLab.Api/Data/Migrations/20260902062130_Lab03Identity.Designer.cs:168 | Метадані конкурентного доступу EF Core | Не секрет: позначення властивості для контролю конкурентних змін, без значення credential. |
| src/SecureLab.Api/Data/Migrations/20260902062130_Lab03Identity.Designer.cs:197 | Назва поля password hash | Не містить hash: оголошення колонки/властивості або перевірка її відсутності; фактичний hash не задано. |
| src/SecureLab.Api/Data/Migrations/20260902062130_Lab03Identity.Designer.cs:412 | Метадані таблиці або типу Identity | Не секрет: назва таблиці, типу, ключа чи зовнішнього ключа; значення токенів у рядку відсутні. |
| src/SecureLab.Api/Data/Migrations/20260902062130_Lab03Identity.cs:52 | Назва поля password hash | Не містить hash: оголошення колонки/властивості або перевірка її відсутності; фактичний hash не задано. |
| src/SecureLab.Api/Data/Migrations/20260902062130_Lab03Identity.cs:154 | Метадані таблиці або типу Identity | Не секрет: назва таблиці, типу, ключа чи зовнішнього ключа; значення токенів у рядку відсутні. |
| src/SecureLab.Api/Data/Migrations/20260902062130_Lab03Identity.cs:164 | Метадані таблиці або типу Identity | Не секрет: назва таблиці, типу, ключа чи зовнішнього ключа; значення токенів у рядку відсутні. |
| src/SecureLab.Api/Data/Migrations/20260902062130_Lab03Identity.cs:166 | Метадані таблиці або типу Identity | Не секрет: назва таблиці, типу, ключа чи зовнішнього ключа; значення токенів у рядку відсутні. |
| src/SecureLab.Api/Data/Migrations/20260902062130_Lab03Identity.cs:210 | Коментар про підготовку seed | Не секрет: пояснює provisioning паролів через seed, без конкретного значення. |
| src/SecureLab.Api/Data/Migrations/SecureLabDbContextModelSnapshot.cs:32 | Метадані конкурентного доступу EF Core | Не секрет: позначення властивості для контролю конкурентних змін, без значення credential. |
| src/SecureLab.Api/Data/Migrations/SecureLabDbContextModelSnapshot.cs:136 | Метадані таблиці або типу Identity | Не секрет: назва таблиці, типу, ключа чи зовнішнього ключа; значення токенів у рядку відсутні. |
| src/SecureLab.Api/Data/Migrations/SecureLabDbContextModelSnapshot.cs:152 | Метадані таблиці або типу Identity | Не секрет: назва таблиці, типу, ключа чи зовнішнього ключа; значення токенів у рядку відсутні. |
| src/SecureLab.Api/Data/Migrations/SecureLabDbContextModelSnapshot.cs:165 | Метадані конкурентного доступу EF Core | Не секрет: позначення властивості для контролю конкурентних змін, без значення credential. |
| src/SecureLab.Api/Data/Migrations/SecureLabDbContextModelSnapshot.cs:194 | Назва поля password hash | Не містить hash: оголошення колонки/властивості або перевірка її відсутності; фактичний hash не задано. |
| src/SecureLab.Api/Data/Migrations/SecureLabDbContextModelSnapshot.cs:409 | Метадані таблиці або типу Identity | Не секрет: назва таблиці, типу, ключа чи зовнішнього ключа; значення токенів у рядку відсутні. |
| src/SecureLab.Api/Data/SecureLabDbContext.cs:29 | Метадані таблиці або типу Identity | Не секрет: назва таблиці, типу, ключа чи зовнішнього ключа; значення токенів у рядку відсутні. |
| src/SecureLab.Api/Data/SecureLabDesignTimeDbContextFactory.cs:9 | Назва або використання конфігурації БД | Не literal credential: ключ, змінна чи передавання connection string провайдеру. Рядки з навчальним значенням класифіковано окремо. |
| src/SecureLab.Api/Data/SecureLabDesignTimeDbContextFactory.cs:10 | Відкриті навчальні credentials БД | accepted, study-only: локальна навчальна БД; узгоджено з README та loopback-прив’язкою Compose. Не реальний production-секрет; повторне використання поза стендом заборонене. |
| src/SecureLab.Api/Data/SecureLabDesignTimeDbContextFactory.cs:14 | Назва або використання конфігурації БД | Не literal credential: ключ, змінна чи передавання connection string провайдеру. Рядки з навчальним значенням класифіковано окремо. |
| src/SecureLab.Api/Data/SecureLabDesignTimeDbContextFactory.cs:15 | Назва або використання конфігурації БД | Не literal credential: ключ, змінна чи передавання connection string провайдеру. Рядки з навчальним значенням класифіковано окремо. |
| src/SecureLab.Api/Data/SecureLabDesignTimeDbContextFactory.cs:17 | Назва або використання конфігурації БД | Не literal credential: ключ, змінна чи передавання connection string провайдеру. Рядки з навчальним значенням класифіковано окремо. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:82 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:119 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:130 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:136 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:161 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:174 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:205 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:236 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:246 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:250 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:284 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:293 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:361 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:382 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:390 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:431 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs:458 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Program.cs:22 | Ім’я antiforgery-заголовка | Не секрет: публічна назва заголовка, а не значення request token. |
| src/SecureLab.Api/Program.cs:33 | Назва або використання конфігурації БД | Не literal credential: ключ, змінна чи передавання connection string провайдеру. Рядки з навчальним значенням класифіковано окремо. |
| src/SecureLab.Api/Program.cs:38 | Назва або використання конфігурації БД | Не literal credential: ключ, змінна чи передавання connection string провайдеру. Рядки з навчальним значенням класифіковано окремо. |
| src/SecureLab.Api/Program.cs:123 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Program.cs:245 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Program.cs:246 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Program.cs:257 | Маршрут видачі antiforgery-токена | Не секрет: URL endpoint видачі токена; фактичного значення токена в коді немає. |
| src/SecureLab.Api/Program.cs:261 | Виклик стандартного antiforgery API | Токени формуються під час виконання; у рядку немає hardcoded token. Їхні фактичні значення не включати до логів і звіту. |
| src/SecureLab.Api/Program.cs:265 | Повернення runtime request token | Не hardcoded-секрет: токен повертається поточному клієнту штатним flow; перевірка доступу й кешування - окремі controls. |
| src/SecureLab.Api/Scaffolding/Lab02Endpoints.cs:18 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Scaffolding/Lab02Endpoints.cs:88 | Керування скасуванням операції | Не секрет: параметр CancellationToken або передавання його в async-операцію; не токен автентифікації. |
| src/SecureLab.Api/Scaffolding/Lab03Identity.cs:17 | Параметр парольної політики | Не секрет: задано мінімальну довжину пароля; значення конкретного пароля відсутнє. |
| src/SecureLab.Api/Scaffolding/Lab03Identity.cs:18 | Реєстрація сервісів Identity | Не секрет: підключення стандартних провайдерів токенів; ключі та значення токенів тут не задано. |
| src/SecureLab.Api/Scaffolding/Lab03Identity.cs:48 | Перевірка вхідного пароля | Обробка чутливих даних під час виконання; у рядку лише перевірка порожнього значення, без hardcoded пароля. |
| src/SecureLab.Api/Scaffolding/Lab03Identity.cs:102 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| src/SecureLab.Api/Scaffolding/Lab03Identity.cs:138 | Перевірка вхідного пароля | Обробка чутливих даних під час виконання; у рядку лише перевірка порожнього значення, без hardcoded пароля. |
| src/SecureLab.Api/Scaffolding/Lab03Identity.cs:149 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| src/SecureLab.Api/Scaffolding/Lab03Identity.cs:151 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| src/SecureLab.Api/Scaffolding/Lab03Identity.cs:215 | Параметр або поле пароля | Оголошення типу/параметра для runtime-вводу; значення credential у коді відсутнє. |
| src/SecureLab.Api/Scaffolding/Lab03Identity.cs:216 | Параметр або поле пароля | Оголошення типу/параметра для runtime-вводу; значення credential у коді відсутнє. |
| src/SecureLab.Api/appsettings.Development.json:2 | Назва або використання конфігурації БД | Не literal credential: ключ, змінна чи передавання connection string провайдеру. Рядки з навчальним значенням класифіковано окремо. |
| src/SecureLab.Api/appsettings.Development.json:3 | Відкриті навчальні credentials БД | accepted, study-only: локальна навчальна БД; узгоджено з README та loopback-прив’язкою Compose. Не реальний production-секрет; повторне використання поза стендом заборонене. |
| tests/SecureLab.Api.Tests/AuthenticationMechanicsTests.cs:10 | Читання пароля поза Git | У коді лише ім’я ключа SeedUsers:Password / SeedUsers__Password; фактичне значення надходить із конфігурації середовища. |
| tests/SecureLab.Api.Tests/AuthenticationMechanicsTests.cs:11 | Діагностичне повідомлення про конфігурацію | Названо відсутній ключ або вимогу задати його поза Git; фактичний пароль у повідомленні не наведено. |
| tests/SecureLab.Api.Tests/AuthenticationMechanicsTests.cs:12 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:46 | Helper отримання seed-пароля | Метод або його виклик, без literal пароля; джерело Environment.GetEnvironmentVariable перевірено окремим збігом. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:74 | Helper отримання seed-пароля | Метод або його виклик, без literal пароля; джерело Environment.GetEnvironmentVariable перевірено окремим збігом. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:115 | Helper отримання seed-пароля | Метод або його виклик, без literal пароля; джерело Environment.GetEnvironmentVariable перевірено окремим збігом. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:116 | Отримання токена тестовим клієнтом | Не секрет у коді: helper одержує реальний токен під час виконання тим самим cookie-клієнтом; значення не задано літералом. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:138 | Негативний authentication-тест | Не hardcoded credential: назва тесту/відповіді або модифікація runtime seed-пароля для невдалого входу; базовий пароль надходить поза Git. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:148 | Helper отримання seed-пароля | Метод або його виклик, без literal пароля; джерело Environment.GetEnvironmentVariable перевірено окремим збігом. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:156 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:160 | Негативний authentication-тест | Не hardcoded credential: назва тесту/відповіді або модифікація runtime seed-пароля для невдалого входу; базовий пароль надходить поза Git. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:165 | Негативний authentication-тест | Не hardcoded credential: назва тесту/відповіді або модифікація runtime seed-пароля для невдалого входу; базовий пароль надходить поза Git. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:174 | Негативний authentication-тест | Не hardcoded credential: назва тесту/відповіді або модифікація runtime seed-пароля для невдалого входу; базовий пароль надходить поза Git. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:178 | Негативний authentication-тест | Не hardcoded credential: назва тесту/відповіді або модифікація runtime seed-пароля для невдалого входу; базовий пароль надходить поза Git. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:181 | Негативний authentication-тест | Не hardcoded credential: назва тесту/відповіді або модифікація runtime seed-пароля для невдалого входу; базовий пароль надходить поза Git. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:184 | Негативний authentication-тест | Не hardcoded credential: назва тесту/відповіді або модифікація runtime seed-пароля для невдалого входу; базовий пароль надходить поза Git. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:196 | Helper отримання seed-пароля | Метод або його виклик, без literal пароля; джерело Environment.GetEnvironmentVariable перевірено окремим збігом. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:197 | Читання пароля поза Git | У коді лише ім’я ключа SeedUsers:Password / SeedUsers__Password; фактичне значення надходить із конфігурації середовища. |
| tests/SecureLab.Api.Tests/AuthenticationTests.cs:199 | Діагностичне повідомлення про конфігурацію | Названо відсутній ключ або вимогу задати його поза Git; фактичний пароль у повідомленні не наведено. |
| tests/SecureLab.Api.Tests/CookieClient.cs:8 | Параметр або поле пароля | Оголошення типу/параметра для runtime-вводу; значення credential у коді відсутнє. |
| tests/SecureLab.Api.Tests/CookieClient.cs:14 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/CookieSecurityTests.cs:19 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/CookieSecurityTests.cs:20 | Читання пароля поза Git | У коді лише ім’я ключа SeedUsers:Password / SeedUsers__Password; фактичне значення надходить із конфігурації середовища. |
| tests/SecureLab.Api.Tests/CookieSecurityTests.cs:22 | Діагностичне повідомлення про конфігурацію | Названо відсутній ключ або вимогу задати його поза Git; фактичний пароль у повідомленні не наведено. |
| tests/SecureLab.Api.Tests/CookieSecurityTests.cs:29 | Назва або використання конфігурації БД | Не literal credential: ключ, змінна чи передавання connection string провайдеру. Рядки з навчальним значенням класифіковано окремо. |
| tests/SecureLab.Api.Tests/CookieSecurityTests.cs:31 | Відкриті навчальні credentials БД | accepted, study-only: локальна навчальна БД; узгоджено з README та loopback-прив’язкою Compose. Не реальний production-секрет; повторне використання поза стендом заборонене. |
| tests/SecureLab.Api.Tests/CookieSecurityTests.cs:54 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/CookieSecurityTests.cs:86 | Змінна відповіді або перевірка antiforgery | Не hardcoded-секрет: змінна містить runtime HTTP-відповідь або перевіряє status/headers. Значення cookie й token не наведено в рядку. |
| tests/SecureLab.Api.Tests/CookieSecurityTests.cs:87 | Маршрут видачі antiforgery-токена | Не секрет: URL endpoint видачі токена; фактичного значення токена в коді немає. |
| tests/SecureLab.Api.Tests/CookieSecurityTests.cs:89 | Змінна відповіді або перевірка antiforgery | Не hardcoded-секрет: змінна містить runtime HTTP-відповідь або перевіряє status/headers. Значення cookie й token не наведено в рядку. |
| tests/SecureLab.Api.Tests/CookieSecurityTests.cs:92 | Змінна відповіді або перевірка antiforgery | Не hardcoded-секрет: змінна містить runtime HTTP-відповідь або перевіряє status/headers. Значення cookie й token не наведено в рядку. |
| tests/SecureLab.Api.Tests/IncidentCommentAuthorizationTests.cs:25 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/IncidentCommentAuthorizationTests.cs:26 | Читання пароля поза Git | У коді лише ім’я ключа SeedUsers:Password / SeedUsers__Password; фактичне значення надходить із конфігурації середовища. |
| tests/SecureLab.Api.Tests/IncidentCommentAuthorizationTests.cs:28 | Перевірка вхідного пароля | Обробка чутливих даних під час виконання; у рядку лише перевірка порожнього значення, без hardcoded пароля. |
| tests/SecureLab.Api.Tests/IncidentCommentAuthorizationTests.cs:31 | Діагностичне повідомлення про конфігурацію | Названо відсутній ключ або вимогу задати його поза Git; фактичний пароль у повідомленні не наведено. |
| tests/SecureLab.Api.Tests/IncidentCommentAuthorizationTests.cs:34 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/IncidentCreateTest.cs:17 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/IncidentCreateTest.cs:18 | Читання пароля поза Git | У коді лише ім’я ключа SeedUsers:Password / SeedUsers__Password; фактичне значення надходить із конфігурації середовища. |
| tests/SecureLab.Api.Tests/IncidentCreateTest.cs:20 | Діагностичне повідомлення про конфігурацію | Названо відсутній ключ або вимогу задати його поза Git; фактичний пароль у повідомленні не наведено. |
| tests/SecureLab.Api.Tests/IncidentCreateTest.cs:22 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/IncidentCreateTest.cs:23 | Отримання токена тестовим клієнтом | Не секрет у коді: helper одержує реальний токен під час виконання тим самим cookie-клієнтом; значення не задано літералом. |
| tests/SecureLab.Api.Tests/IncidentCreateTest.cs:203 | Маркер перевірки відсутності витоку | Не секрет: назва конфігурації використовується як заборонена ознака в перевірці response; credential не задано. |
| tests/SecureLab.Api.Tests/IncidentCreateTest.cs:205 | Маркер перевірки відсутності витоку | Не секрет: фрагмент формату connection string для негативної перевірки відповіді; після знака рівності значення відсутнє. |
| tests/SecureLab.Api.Tests/IncidentEndpointTests.cs:18 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/IncidentEndpointTests.cs:19 | Читання пароля поза Git | У коді лише ім’я ключа SeedUsers:Password / SeedUsers__Password; фактичне значення надходить із конфігурації середовища. |
| tests/SecureLab.Api.Tests/IncidentEndpointTests.cs:21 | Перевірка вхідного пароля | Обробка чутливих даних під час виконання; у рядку лише перевірка порожнього значення, без hardcoded пароля. |
| tests/SecureLab.Api.Tests/IncidentEndpointTests.cs:24 | Діагностичне повідомлення про конфігурацію | Названо відсутній ключ або вимогу задати його поза Git; фактичний пароль у повідомленні не наведено. |
| tests/SecureLab.Api.Tests/IncidentEndpointTests.cs:27 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/IncidentMutationAuthorizationTests.cs:17 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/IncidentMutationAuthorizationTests.cs:18 | Читання пароля поза Git | У коді лише ім’я ключа SeedUsers:Password / SeedUsers__Password; фактичне значення надходить із конфігурації середовища. |
| tests/SecureLab.Api.Tests/IncidentMutationAuthorizationTests.cs:20 | Перевірка вхідного пароля | Обробка чутливих даних під час виконання; у рядку лише перевірка порожнього значення, без hardcoded пароля. |
| tests/SecureLab.Api.Tests/IncidentMutationAuthorizationTests.cs:23 | Діагностичне повідомлення про конфігурацію | Названо відсутній ключ або вимогу задати його поза Git; фактичний пароль у повідомленні не наведено. |
| tests/SecureLab.Api.Tests/IncidentMutationAuthorizationTests.cs:26 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/IncidentMutationAuthorizationTests.cs:28 | Отримання токена тестовим клієнтом | Не секрет у коді: helper одержує реальний токен під час виконання тим самим cookie-клієнтом; значення не задано літералом. |
| tests/SecureLab.Api.Tests/IncidentOwnershipTests.cs:19 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/IncidentOwnershipTests.cs:20 | Читання пароля поза Git | У коді лише ім’я ключа SeedUsers:Password / SeedUsers__Password; фактичне значення надходить із конфігурації середовища. |
| tests/SecureLab.Api.Tests/IncidentOwnershipTests.cs:22 | Діагностичне повідомлення про конфігурацію | Названо відсутній ключ або вимогу задати його поза Git; фактичний пароль у повідомленні не наведено. |
| tests/SecureLab.Api.Tests/IncidentOwnershipTests.cs:24 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/IncidentOwnershipTests.cs:25 | Отримання токена тестовим клієнтом | Не секрет у коді: helper одержує реальний токен під час виконання тим самим cookie-клієнтом; значення не задано літералом. |
| tests/SecureLab.Api.Tests/IncidentSearchTests.cs:17 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/IncidentSearchTests.cs:18 | Читання пароля поза Git | У коді лише ім’я ключа SeedUsers:Password / SeedUsers__Password; фактичне значення надходить із конфігурації середовища. |
| tests/SecureLab.Api.Tests/IncidentSearchTests.cs:20 | Діагностичне повідомлення про конфігурацію | Названо відсутній ключ або вимогу задати його поза Git; фактичний пароль у повідомленні не наведено. |
| tests/SecureLab.Api.Tests/IncidentSearchTests.cs:22 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/IncidentSearchTests.cs:23 | Отримання токена тестовим клієнтом | Не секрет у коді: helper одержує реальний токен під час виконання тим самим cookie-клієнтом; значення не задано літералом. |
| tests/SecureLab.Api.Tests/LoginRateLimitTests.cs:56 | Штучне значення негативного тесту | accepted, test-only: вигаданий пароль у тесті login limiter для невідомого користувача; не credential навчальних акаунтів. |
| tests/SecureLab.Api.Tests/RegistrationTests.cs:19 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/RegistrationTests.cs:20 | Читання пароля поза Git | У коді лише ім’я ключа SeedUsers:Password / SeedUsers__Password; фактичне значення надходить із конфігурації середовища. |
| tests/SecureLab.Api.Tests/RegistrationTests.cs:22 | Діагностичне повідомлення про конфігурацію | Названо відсутній ключ або вимогу задати його поза Git; фактичний пароль у повідомленні не наведено. |
| tests/SecureLab.Api.Tests/RegistrationTests.cs:40 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/RegistrationTests.cs:76 | Runtime-обробка пароля | Змінна, аргумент, поле DTO або виклик Identity/тестового входу; конкретний пароль у наведеному рядку не задано. Не логувати його значення. |
| tests/SecureLab.Api.Tests/packages.lock.json:208 | Метадані NuGet-залежності | Не секрет: назва пакета Microsoft.Extensions.Configuration.UserSecrets або посилання на його версію; секретних значень немає. |
| tests/SecureLab.Api.Tests/packages.lock.json:291 | Метадані NuGet-залежності | Не секрет: назва пакета Microsoft.Extensions.Configuration.UserSecrets або посилання на його версію; секретних значень немає. |

## Залишкові межі й окремі дії

1. Нові untracked файли не входять до цього `git grep`. Зокрема, якщо `csrf.js`, `AntiforgeryFilter.cs` та `AntiforgeryTestExtensions.cs` ще не додані до Git, їх потрібно переглянути окремо або повторити пошук після додавання. За цим виводом їхній повний локальний вміст не перевірено.
2. `docs/`, інші каталоги, локальні файли конфігурації, історія Git, stash, інші клони, зовнішні CI та логи не входили до наведеної команди. Їх не позначено як перевірені цим документом. Окремий огляд логів дав нуль збігів для seed-пароля, Cookie:, Set-Cookie:, X-CSRF-TOKEN: та тестового body marker; він стосується лише переглянутого локального sink і сценаріїв.
3. Пошук за ключовими словами не знаходить гарантовано всі види секретів: значення можуть мати інші назви, бути закодованими або розташовуватися поза вказаними файлами. Рішення стосуються наданих збігів і навчального контексту.
4. Для навчальної БД прийнятність обмежена loopback-прив’язкою `127.0.0.1` і забороною повторного використання, задокументованими у проєкті. Зміна цих передумов потребує нового рішення щодо credentials.
5. Якщо буде виявлено реальний credential, не включати його до доказів. Видалення з поточного файла не усуває попередній витік: потрібні відкликання/ротація та оцінка доступних копій й історії.
