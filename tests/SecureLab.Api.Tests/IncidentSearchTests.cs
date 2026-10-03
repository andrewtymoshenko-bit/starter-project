using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

public sealed class IncidentSearchTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("%", "X")]
    [InlineData("_", "X")]
    [InlineData("\\", "")]
    [InlineData("'", "")]
    public async Task Search_SpecialCharacter_IsLiteral(
        string character,
        string replacement)
    {
        var prefix = $"LR02-search-{Guid.NewGuid():N}-";

        var literalTitle = prefix + character + "end";
        var controlTitle = prefix + replacement + "end";

        try
        {
            var literalId = await CreateAsync(literalTitle);
            var controlId = await CreateAsync(controlTitle);

            // Обидва записи існують і знаходяться за спільним префіксом.
            var commonIds = await SearchAsync(prefix);

            Assert.Equal(2, commonIds.Length);
            Assert.Contains(literalId, commonIds);
            Assert.Contains(controlId, commonIds);

            // Повний текст зі спеціальним символом знаходить лише
            // запис, який справді містить цей символ.
            var literalIds = await SearchAsync(literalTitle);

            Assert.Single(literalIds);
            Assert.Equal(literalId, literalIds[0]);
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider
                .GetRequiredService<SecureLabDbContext>();

            await db.Incidents
                .Where(incident =>
                    incident.Title == literalTitle
                    || incident.Title == controlTitle)
                .ExecuteDeleteAsync();
        }
    }

    private async Task<Guid> CreateAsync(string title)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title,
                description = "Навчальний запис для перевірки пошуку.",
                severity = "Low",
                occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid[]> SearchAsync(string text)
    {
        var encoded = Uri.EscapeDataString(text);

        using var response = await _client.GetAsync(
            $"/api/incidents/search?q={encoded}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement
            .EnumerateArray()
            .Select(incident => incident.GetProperty("id").GetGuid())
            .ToArray();
    }
}