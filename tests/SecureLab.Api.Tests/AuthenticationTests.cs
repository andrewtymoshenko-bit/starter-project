using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SecureLab.Api.Data;
using System.Net.Http.Json;

namespace SecureLab.Api.Tests;

public sealed class AuthenticationTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task A01_AnonymousMe_Returns401WithoutRedirect()
    {
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });

        using var response = await client.GetAsync("/api/me");

        using var createResponse = await client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "Anonymous-" + Guid.NewGuid().ToString("N"),
                description = "Перевірка створення інциденту без автентифікації.",
                severity = "Medium",
                occurredAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task A02_LoginAsBob_ReturnsVerifiedSafeProfile()
    {
        using var client = await factory.LoginAsync(
            "bob",
            GetSeedPassword());

        using var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        AssertSafeProfile(body.RootElement);

        Assert.Equal(
            DbSeeder.BobId,
            body.RootElement.GetProperty("id").GetGuid());

        Assert.Equal(
            "bob",
            body.RootElement.GetProperty("userName").GetString());
    }

    [Fact]
    public async Task A03_ForgedDemoHeader_DoesNotChangeProfile()
    {
        using var client = await factory.LoginAsync(
            "bob",
            GetSeedPassword());

        using var before = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using var beforeBody = JsonDocument.Parse(
            await before.Content.ReadAsStringAsync());

        AssertSafeProfile(beforeBody.RootElement);

        Assert.Equal(
            DbSeeder.BobId,
            beforeBody.RootElement.GetProperty("id").GetGuid());

        // Сеанс належить Bob, але header заявляє Alice.
        client.DefaultRequestHeaders.Add(
            "X-Demo-UserId",
            DbSeeder.AliceId.ToString());

        using var after = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, after.StatusCode);

        using var afterBody = JsonDocument.Parse(
            await after.Content.ReadAsStringAsync());

        AssertSafeProfile(afterBody.RootElement);

        foreach (var field in new[] { "id", "userName", "displayName" })
        {
            Assert.Equal(
                beforeBody.RootElement.GetProperty(field).GetString(),
                afterBody.RootElement.GetProperty(field).GetString());
        }
    }
    [Fact]
    public async Task A06_Logout_SameClientThenReceives401()
    {
        using var client = await factory.LoginAsync(
            "bob",
            GetSeedPassword());

        // До виходу захищений endpoint доступний.
        using var before = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        // Виходимо тим самим клієнтом.
        using var logout = await client.PostAsync(
            "/api/auth/logout",
            null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // Cookie-клієнт обробив відповідь logout.
        using var after = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Null(after.Headers.Location);
    }

    [Fact]
    public async Task A07_UnknownUserAndWrongPassword_ReturnSameResult()
    {
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });

        var password = GetSeedPassword();

        // Унікальне ім’я користувача, якого немає в БД.
        using var unknownUser = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                userName = "missing" + Guid.NewGuid().ToString("N"),
                password
            });

        // Наявний користувач, але гарантовано інший пароль.
        using var wrongPassword = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                userName = "alice",
                password = password + "!wrong"
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            unknownUser.StatusCode);

        Assert.Equal(
            unknownUser.StatusCode,
            wrongPassword.StatusCode);

        Assert.Equal(
            unknownUser.Content.Headers.ContentType?.ToString(),
            wrongPassword.Content.Headers.ContentType?.ToString());

        Assert.Null(unknownUser.Headers.Location);
        Assert.Null(wrongPassword.Headers.Location);

        var unknownFields = await ReadStableErrorAsync(unknownUser);
        var wrongFields = await ReadStableErrorAsync(wrongPassword);

        Assert.Equal(
            unknownFields.Keys.OrderBy(key => key).ToArray(),
            wrongFields.Keys.OrderBy(key => key).ToArray());

        foreach (var field in unknownFields)
        {
            Assert.Equal(field.Value, wrongFields[field.Key]);
        }
    }

    private static string GetSeedPassword()
        => Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Задайте SeedUsers__Password перед запуском тестів.");

    private static void AssertSafeProfile(JsonElement profile)
    {
        var fields = profile.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            new[] { "displayName", "id", "userName" },
            fields);
    }
    private static async Task<Dictionary<string, string?>>
        ReadStableErrorAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var fields = new Dictionary<string, string?>();

        // Однакові порожні відповіді також можуть бути
        // безпечним контрактом відмови.
        if (string.IsNullOrWhiteSpace(json))
        {
            return fields;
        }

        using var body = JsonDocument.Parse(json);

        Assert.Equal(
            JsonValueKind.Object,
            body.RootElement.ValueKind);

        foreach (var name in new[]
        {
            "type", "title", "status", "detail", "code", "message"
        })
        {
            fields[name] = body.RootElement.TryGetProperty(
                name,
                out var value)
                    ? value.GetRawText()
                    : null;
        }

    return fields;
}
}