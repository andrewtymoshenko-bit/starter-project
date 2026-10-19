using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Tests;

public sealed class IncidentEndpointTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private Task<HttpClient> LoginAsync(string userName)
    {
        var password =
            Environment.GetEnvironmentVariable("SeedUsers__Password");

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Задайте SeedUsers__Password перед запуском тестів.");
        }

        return factory.LoginAsync(userName, password);
    }

    // A-01: анонімний клієнт отримує 401 без перенаправлення.
    [Theory]
    [InlineData("/api/incidents")]
    [InlineData(
        "/api/incidents/20000000-0000-0000-0000-000000000001")]
    [InlineData(
        "/api/incident-comments/30000000-0000-0000-0000-000000000001")]
    public async Task ProtectedRead_AsAnonymous_Returns401WithoutRedirect(
        string url)
    {
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false
            });

        using var response = await client.GetAsync(url);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        Assert.Null(response.Headers.Location);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    // A-05: кожен повернений запис справді належить Alice.
    [Fact]
    public async Task GetList_AsAlice_ReturnsOnlyOwnedIncidents()
    {
        using var client = await LoginAsync("alice");

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<SecureLabDbContext>();

        var allowedIds = await db.Incidents
            .AsNoTracking()
            .Where(incident => incident.OwnerUserId == DbSeeder.AliceId)
            .Select(incident => incident.Id)
            .ToListAsync();

        var incidents =
            await client.GetFromJsonAsync<List<IncidentListItemResponse>>(
                "/api/incidents");

        Assert.NotNull(incidents);

        Assert.Contains(
            incidents,
            incident => incident.Id == DbSeeder.AliceIncidentId);

        Assert.DoesNotContain(
            incidents,
            incident => incident.Id == DbSeeder.BobIncidentId);

        Assert.All(
            incidents,
            incident => Assert.Contains(incident.Id, allowedIds));
    }

    [Fact]
    public async Task GetDetails_ForUnknownId_ReturnsProblemDetails404()
    {
        using var client = await LoginAsync("alice");

        using var response = await client.GetAsync(
            $"/api/incidents/{Guid.NewGuid()}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    // A-02: власний ресурс доступний, внутрішні owner-поля не розкрито.
    [Fact]
    public async Task GetDetails_AsOwner_ReturnsSafeDetails()
    {
        using var client = await LoginAsync("alice");

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var body = document.RootElement;

        Assert.Equal(
            DbSeeder.AliceIncidentId,
            body.GetProperty("id").GetGuid());

        Assert.Equal(
            "Аліса Коваль",
            body.GetProperty("ownerDisplayName").GetString());

        Assert.False(body.TryGetProperty("ownerUserId", out _));
        Assert.False(body.TryGetProperty("email", out _));
    }

    // A-03: Alice не читає інцидент Bob.
    [Fact]
    public async Task GetDetails_AsNonOwner_Returns404WithoutIncidentData()
    {
        using var client = await LoginAsync("alice");

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.BobIncidentId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var body = document.RootElement;

        Assert.Equal(
            "Інцидент не знайдено",
            body.GetProperty("title").GetString());

        Assert.False(body.TryGetProperty("id", out _));
        Assert.False(body.TryGetProperty("description", out _));
        Assert.False(body.TryGetProperty("ownerDisplayName", out _));
        Assert.False(body.TryGetProperty("comments", out _));
    }

    // A-03R: Morgan читає інцидент Alice для аналізу.
    [Fact]
    public async Task GetDetails_AsAnalyst_CanReadAliceIncident()
    {
        using var client = await LoginAsync("morgan");

        using var response = await client.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<IncidentDetailsResponse>();

        Assert.NotNull(body);
        Assert.Equal(DbSeeder.AliceIncidentId, body.Id);
        Assert.Equal("Аліса Коваль", body.OwnerDisplayName);
    }

    // A-10: чужий та неіснуючий ресурс дають однакові відповіді.
    [Fact]
    public async Task GetDetails_ForeignAndUnknown_ReturnEquivalent404()
    {
        using var client = await LoginAsync("alice");

        using var foreign = await client.GetAsync(
            $"/api/incidents/{DbSeeder.BobIncidentId}");

        using var unknown = await client.GetAsync(
            $"/api/incidents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        Assert.Equal(
            unknown.Content.Headers.ContentType?.ToString(),
            foreign.Content.Headers.ContentType?.ToString());

        var foreignBody = JsonNode.Parse(
            await foreign.Content.ReadAsStringAsync())!.AsObject();

        var unknownBody = JsonNode.Parse(
            await unknown.Content.ReadAsStringAsync())!.AsObject();

        // Ідентифікатори різних запитів закономірно відрізняються.
        foreignBody.Remove("traceId");
        unknownBody.Remove("traceId");

        Assert.True(
            JsonNode.DeepEquals(foreignBody, unknownBody),
            "Тіла 404 відрізняються після вилучення traceId.");
    }

    [Fact]
    public async Task ClientScript_DoesNotUseDangerousInnerHtmlSink()
    {
        using var client = factory.CreateClient();

        var script = await client.GetStringAsync("/app.js");

        Assert.DoesNotContain(
            "innerHTML",
            script,
            StringComparison.Ordinal);

        Assert.Contains(
            "textContent",
            script,
            StringComparison.Ordinal);
    }
}