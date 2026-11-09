window.secureLabCsrf = (() => {
    let token = null;

    function reset() {
        token = null;
    }

    async function getToken() {
        if (token) return token;

        const response = await fetch("/api/auth/csrf-token", {
            credentials: "same-origin",
            cache: "no-store"
        });

        if (!response.ok) {
            throw new Error("Не вдалося отримати CSRF-токен.");
        }

        const body = await response.json();

        if (typeof body.token !== "string" || !body.token) {
            throw new Error("Сервер не повернув CSRF-токен.");
        }

        token = body.token;
        return token;
    }

    async function send(path, options = {}) {
        const url = new URL(path, location.origin);

        if (url.origin !== location.origin) {
            throw new Error("Дозволені лише запити до власного API.");
        }

        const method = (options.method ?? "GET").toUpperCase();
        const headers = new Headers(options.headers);

        if (["POST", "PUT", "PATCH", "DELETE"].includes(method)) {
            headers.set("X-CSRF-TOKEN", await getToken());
        }

        const response = await fetch(url, {
            ...options,
            method,
            headers,
            credentials: "same-origin"
        });

        if (response.status === 401) {
            reset();
        }

        return response;
    }

    return { fetch: send, reset };
})();