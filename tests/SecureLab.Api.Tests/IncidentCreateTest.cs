using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

public sealed class IncidentCreateTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // T-02: різні помилки вхідних даних.
    [Theory]
    [InlineData("emptyDescription", "description")]
    [InlineData("longTitle", "title")]
    [InlineData("longDescription", "description")]
    [InlineData("invalidSeverity", "severity")]
    [InlineData("missingDate", "occurredAtUtc")]
    [InlineData("futureDate", "occurredAtUtc")]
    [InlineData("high39", "description")]
    [InlineData("critical39", "description")]
    public async Task Create_InvalidInput_Returns400(
        string scenario,
        string expectedErrorKey)
    {
        var title = $"LR02-invalid-{Guid.NewGuid():N}";
        var description = "Коректний навчальний опис інциденту.";
        var severity = "Low";
        DateTimeOffset? occurredAtUtc =
            DateTimeOffset.UtcNow.AddMinutes(-10);

        switch (scenario)
        {
            case "emptyDescription":
                description = "   ";
                break;

            case "longTitle":
                title += new string('x', 161 - title.Length);
                break;

            case "longDescription":
                description = new string('x', 4001);
                break;

            case "invalidSeverity":
                severity = "7";
                break;

            case "missingDate":
                occurredAtUtc = null;
                break;

            case "futureDate":
                occurredAtUtc = DateTimeOffset.UtcNow.AddHours(1);
                break;

            case "high39":
                severity = "High";
                description = "  " + new string('x', 39) + "  ";
                break;

            case "critical39":
                severity = "Critical";
                description = "  " + new string('x', 39) + "  ";
                break;
        }

        try
        {
            using var response = await _client.PostAsJsonAsync(
                "/api/incidents",
                new { title, description, severity, occurredAtUtc });

            await AssertProblemAsync(
                response,
                HttpStatusCode.BadRequest,
                expectedErrorKey);

            // Відхилений запит не повинен створити запис.
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider
                .GetRequiredService<SecureLabDbContext>();

            Assert.False(await db.Incidents.AnyAsync(
                item => item.Title == title));
        }
        finally
        {
            await CleanupAsync(title);
        }
    }

    // T-03: однакові назви після Trim дають конфлікт.
    [Fact]
    public async Task Create_DuplicateTitle_Returns409()
    {
        var title = $"LR02-duplicate-{Guid.NewGuid():N}";
        var occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);

        try
        {
            using var first = await _client.PostAsJsonAsync(
                "/api/incidents",
                new
                {
                    title,
                    description = "Перший навчальний інцидент.",
                    severity = "Low",
                    occurredAtUtc
                });

            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            using var second = await _client.PostAsJsonAsync(
                "/api/incidents",
                new
                {
                    title = $"  {title}  ",
                    description = "Повторний навчальний інцидент.",
                    severity = "Low",
                    occurredAtUtc
                });

            await AssertProblemAsync(
                second,
                HttpStatusCode.Conflict);

            // Другий запит не повинен створити ще один запис.
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider
                .GetRequiredService<SecureLabDbContext>();

            Assert.Equal(
                1,
                await db.Incidents.CountAsync(
                    item => item.Title == title));
        }
        finally
        {
            await CleanupAsync(title);
        }
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string? expectedErrorKey = null)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(
            (int)expectedStatus,
            root.GetProperty("status").GetInt32());

        Assert.False(string.IsNullOrWhiteSpace(
            root.GetProperty("title").GetString()));

        if (expectedErrorKey is not null)
        {
            var errors = root.GetProperty("errors");
            Assert.True(errors.TryGetProperty(expectedErrorKey, out _));
        }
        else
        {
            Assert.False(string.IsNullOrWhiteSpace(
                root.GetProperty("detail").GetString()));
        }

        // Перевірка відсутності типових внутрішніх деталей.
        foreach (var marker in new[]
        {
            "stackTrace",
            "Npgsql",
            "System.",
            "ConnectionStrings",
            "Host=",
            "Password=",
            "SELECT "
        })
        {
            Assert.DoesNotContain(
                marker,
                json,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task CleanupAsync(string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<SecureLabDbContext>();

        await db.Incidents
            .Where(item => item.Title == title
                || item.Title == $"  {title}  ")
            .ExecuteDeleteAsync();
    }
}