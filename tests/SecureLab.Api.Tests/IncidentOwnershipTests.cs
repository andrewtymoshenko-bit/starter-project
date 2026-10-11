using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

public sealed class IncidentOwnershipTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A04_A05_CreateAsBob_StoresBobAsOwner(
        bool includeForgedOwner)
    {
        var password =
            Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Задайте SeedUsers__Password перед запуском тестів.");

        using var client = await factory.LoginAsync("bob", password);

        var title = "LR03-owner-" + Guid.NewGuid().ToString("N");

        var request = new Dictionary<string, object>
        {
            ["title"] = title,
            ["description"] =
                "Перевірка визначення власника за автентифікованим користувачем.",
            ["severity"] = "Medium",
            ["occurredAtUtc"] = DateTimeOffset.UtcNow.AddDays(-1)
        };

        if (includeForgedOwner)
        {
            request["ownerUserId"] = DbSeeder.AliceId;
        }

        try
        {
            using var response = await client.PostAsJsonAsync(
                "/api/incidents",
                request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            using var body = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            var incidentId = body.RootElement
                .GetProperty("id")
                .GetGuid();

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider
                .GetRequiredService<SecureLabDbContext>();

            var savedIncident = await db.Incidents
                .AsNoTracking()
                .SingleAsync(incident => incident.Id == incidentId);

            Assert.Equal(title, savedIncident.Title);
            Assert.Equal(DbSeeder.BobId, savedIncident.OwnerUserId);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider
                .GetRequiredService<SecureLabDbContext>();

            await db.Incidents
                .Where(incident => incident.Title == title)
                .ExecuteDeleteAsync();
        }
    }
}