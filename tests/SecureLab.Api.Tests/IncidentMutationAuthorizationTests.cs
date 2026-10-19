using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Tests;

public sealed class IncidentMutationAuthorizationTests(
    SecureLabApiFactory factory)
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

    // A-04: Alice не змінює і не видаляє інцидент Bob.
    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Mutate_AsNonOwner_Returns404_AndPreservesState(
        string method)
    {
        using var alice = await LoginAsync("alice");

        var before = await ReadStateAsync(DbSeeder.BobIncidentId);
        var url = $"/api/incidents/{DbSeeder.BobIncidentId}";

        using var response = method == "PUT"
            ? await alice.PutAsJsonAsync(url, new
            {
                title = "Спроба зміни від Alice",
                description = "Ця зміна не повинна зберегтися."
            })
            : await alice.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var after = await ReadStateAsync(DbSeeder.BobIncidentId);

        Assert.Equal(before, after);
    }

    // A-08a і позитивна перевірка owner update.
    [Fact]
    public async Task Update_AsOwner_ChangesContent_ButIgnoresProtectedFields()
    {
        using var alice = await LoginAsync("alice");
        var id = await CreateIncidentAsync(alice);

        try
        {
            var before = await ReadStateAsync(id);
            var rolesBefore = await ReadAliceRolesAsync();

            Assert.Contains(DbSeeder.ReporterRole, rolesBefore);

            const string newTitle = "Дозволене редагування власником";
            const string newDescription =
                "Змінено лише дозволений текст інциденту.";

            using var response = await alice.PutAsJsonAsync(
                $"/api/incidents/{id}",
                new
                {
                    title = newTitle,
                    description = newDescription,

                    // Цих полів немає в UpdateIncidentRequest.
                    ownerUserId = DbSeeder.BobId,
                    role = DbSeeder.AnalystRole,
                    status = "Closed",
                    severity = "Critical",
                    id = Guid.NewGuid(),
                    createdAtUtc = "2000-01-01T00:00:00Z",
                    updatedAtUtc = "2000-01-01T00:00:00Z",
                    isTransitionAllowed = true
                });

            Assert.Equal(
                HttpStatusCode.NoContent,
                response.StatusCode);

            var after = await ReadStateAsync(id);

            // Дозволяємо зміну content та серверного UpdatedAtUtc.
            // Усі інші поля й кількості дочірніх записів мають збігатися.
            Assert.Equal(
                before with
                {
                    Title = newTitle,
                    Description = newDescription,
                    UpdatedAtUtc = after.UpdatedAtUtc
                },
                after);

            Assert.True(after.UpdatedAtUtc >= before.UpdatedAtUtc);

            var rolesAfter = await ReadAliceRolesAsync();

            Assert.Equal(rolesBefore, rolesAfter);
        }
        finally
        {
            await DeleteFixtureAsync(id);
        }
    }

    // A-06: навіть власник-Reporter не має права виконувати triage.
    [Fact]
    public async Task Triage_AsReporter_Returns403_AndPreservesState()
    {
        using var alice = await LoginAsync("alice");
        var id = await CreateIncidentAsync(alice);

        try
        {
            var before = await ReadStateAsync(id);

            Assert.Equal(IncidentStatus.New, before.Status);

            using var response = await alice.PostAsJsonAsync(
                $"/api/incidents/{id}/triage",
                new
                {
                    note = "Спроба Reporter виконати triage.",
                    role = DbSeeder.AnalystRole,
                    status = "Triaged",
                    isTransitionAllowed = true
                });

            Assert.Equal(
                HttpStatusCode.Forbidden,
                response.StatusCode);

            var after = await ReadStateAsync(id);

            Assert.Equal(before, after);
        }
        finally
        {
            await DeleteFixtureAsync(id);
        }
    }

    // A-07: Analyst виконує New → Triaged.
    // Повторний перехід відхиляється без нових змін.
    [Fact]
    public async Task Triage_AsAnalyst_ChangesNewToTriaged_OnlyOnce()
    {
        using var alice = await LoginAsync("alice");
        using var morgan = await LoginAsync("morgan");

        var id = await CreateIncidentAsync(alice);

        try
        {
            var before = await ReadStateAsync(id);

            Assert.Equal(IncidentStatus.New, before.Status);

            using var response = await morgan.PostAsJsonAsync(
                $"/api/incidents/{id}/triage",
                new { note = "Первинну оцінку виконав Analyst." });

            Assert.Equal(
                HttpStatusCode.NoContent,
                response.StatusCode);

            var after = await ReadStateAsync(id);

            Assert.Equal(
                before with
                {
                    Status = IncidentStatus.Triaged,
                    UpdatedAtUtc = after.UpdatedAtUtc,
                    HistoryCount = before.HistoryCount + 1
                },
                after);

            using var scope = factory.Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<SecureLabDbContext>();

            var history = await db.IncidentStatusHistory
                .AsNoTracking()
                .SingleAsync(item => item.IncidentId == id);

            Assert.Equal(DbSeeder.MorganId, history.ChangedByUserId);
            Assert.Equal(IncidentStatus.New, history.OldStatus);
            Assert.Equal(IncidentStatus.Triaged, history.NewStatus);

            using var repeated = await morgan.PostAsJsonAsync(
                $"/api/incidents/{id}/triage",
                new { note = "Повторна спроба переходу." });

            Assert.Equal(
                HttpStatusCode.Conflict,
                repeated.StatusCode);

            Assert.Equal(after, await ReadStateAsync(id));
        }
        finally
        {
            await DeleteFixtureAsync(id);
        }
    }

    // Read-дозвіл Analyst не дає права content update або delete.
    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Mutate_AsAnalyst_Returns403_AndPreservesState(
        string method)
    {
        using var morgan = await LoginAsync("morgan");

        var before = await ReadStateAsync(DbSeeder.BobIncidentId);
        var url = $"/api/incidents/{DbSeeder.BobIncidentId}";

        using var response = method == "PUT"
            ? await morgan.PutAsJsonAsync(url, new
            {
                title = "Спроба зміни від Analyst",
                description = "Право читання не дозволяє цю зміну."
            })
            : await morgan.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        Assert.Equal(
            before,
            await ReadStateAsync(DbSeeder.BobIncidentId));
    }

    private static async Task<Guid> CreateIncidentAsync(
        HttpClient owner)
    {
        using var response = await owner.PostAsJsonAsync(
            "/api/incidents",
            new
            {
                title = "LR04-test-" + Guid.NewGuid().ToString("N"),
                description = "Інцидент, створений окремим тестом.",
                severity = "Medium",
                occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<IncidentState> ReadStateAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<SecureLabDbContext>();

        return await db.Incidents
            .AsNoTracking()
            .Where(incident => incident.Id == id)
            .Select(incident => new IncidentState(
                incident.OwnerUserId,
                incident.Title,
                incident.Description,
                incident.Severity,
                incident.Status,
                incident.OccurredAtUtc,
                incident.CreatedAtUtc,
                incident.UpdatedAtUtc,
                incident.Comments.Count,
                incident.StatusHistory.Count))
            .SingleAsync();
    }

    private async Task<string[]> ReadAliceRolesAsync()
    {
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<SecureLabDbContext>();

        return await (
            from assignment in db.UserRoles
            join role in db.Roles on assignment.RoleId equals role.Id
            where assignment.UserId == DbSeeder.AliceId
            orderby role.Name
            select role.Name!)
            .ToArrayAsync();
    }

    private async Task DeleteFixtureAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<SecureLabDbContext>();

        await db.IncidentComments
            .Where(comment => comment.IncidentId == id)
            .ExecuteDeleteAsync();

        await db.IncidentStatusHistory
            .Where(history => history.IncidentId == id)
            .ExecuteDeleteAsync();

        await db.Incidents
            .Where(incident => incident.Id == id)
            .ExecuteDeleteAsync();
    }

    private sealed record IncidentState(
        Guid OwnerUserId,
        string Title,
        string Description,
        IncidentSeverity Severity,
        IncidentStatus Status,
        DateTimeOffset OccurredAtUtc,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        int CommentCount,
        int HistoryCount);
}