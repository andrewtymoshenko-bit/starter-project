const result = document.querySelector("#result");

const sessionState = document.querySelector("#session-state");
const profile = document.querySelector("#profile");
const createButton = document.querySelector(
  "#create-incident button[type='submit']"
);

function showSession(state, user = null) {
  profile.textContent = "";
  createButton.disabled = state !== "authenticated";

  if (state === "authenticated") {
    sessionState.textContent = "Ви увійшли в систему.";

    profile.textContent = JSON.stringify({
      id: user.id,
      userName: user.userName,
      displayName: user.displayName
    }, null, 2);
  } else if (state === "anonymous") {
    sessionState.textContent = "Ви не увійшли в систему.";
  } else {
    sessionState.textContent = "Стан сеансу невідомий.";
  }
}

async function refreshSession() {
  showSession("unknown");

  try {
    const response = await fetch("/api/me", {
      credentials: "same-origin",
      cache: "no-store"
    });

    if (response.status === 200) {
      const user = await response.json();
      showSession("authenticated", user);
    } else if (response.status === 401) {
      showSession("anonymous");
    }

    return response.status;
  } catch {
    showSession("unknown");
    return null;
  }
}

document.querySelector("#login")
  .addEventListener("submit", async event => {
    event.preventDefault();

    const form = event.currentTarget;

    try {
      const response = await fetch("/api/auth/login", {
        method: "POST",
        credentials: "same-origin",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(
          Object.fromEntries(new FormData(form))
        )
      });

      result.textContent = `Login: ${response.status}`;

      await refreshSession();
    } catch {
      result.textContent = "Не вдалося виконати вхід.";
      showSession("unknown");
    } finally {
      form.elements.password.value = "";
    }
  });

document.querySelector("#me")
  .addEventListener("click", async () => {
    const status = await refreshSession();

    result.textContent = status === null
      ? "Не вдалося перевірити сеанс."
      : `Me: ${status}`;
  });

document.querySelector("#logout")
  .addEventListener("click", async () => {
    try {
      const response = await fetch("/api/auth/logout", {
        method: "POST",
        credentials: "same-origin"
      });

      result.textContent = `Logout: ${response.status}`;

      if (response.status === 204) {
        showSession("anonymous");
        document.querySelector("#create-result").textContent = "";
        document.querySelector("#create-incident").reset();
      }

      await refreshSession();
    } catch {
      result.textContent = "Не вдалося виконати вихід.";
      showSession("unknown");
    }
  });

const createResult = document.querySelector("#create-result");

document.querySelector("#create-incident")
  .addEventListener("submit", async event => {
    event.preventDefault();

    const form = event.currentTarget;
    const fields = new FormData(form);

    const request = {
      title: fields.get("title"),
      description: fields.get("description"),
      severity: fields.get("severity"),
      occurredAtUtc: new Date(Date.now() - 60_000).toISOString()
    };

    try {
      const response = await fetch("/api/incidents", {
        method: "POST",
        credentials: "same-origin",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(request)
      });

      const body = await response.text();

      createResult.textContent =
        `Create: ${response.status}\n${body}`;

      if (response.status === 201) {
        form.reset();
      }
    } catch {
      createResult.textContent =
        "Не вдалося отримати відповідь сервера.";
    }
  });
  refreshSession();

const registerResult = document.querySelector("#register-result");

document.querySelector("#register")
  .addEventListener("submit", async event => {
    event.preventDefault();

    const form = event.currentTarget;
    const button = form.querySelector("button[type='submit']");

    button.disabled = true;

    try {
      const response = await fetch("/api/auth/register", {
        method: "POST",
        credentials: "same-origin",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(
          Object.fromEntries(new FormData(form))
        )
      });

      const body = await response.text();

      registerResult.textContent =
        `Register: ${response.status}\n${body}`;

      if (response.status === 201) {
        form.reset();
      }
    } catch {
      registerResult.textContent =
        "Не вдалося виконати реєстрацію.";
    } finally {
      form.elements.password.value = "";
      button.disabled = false;
    }
  });