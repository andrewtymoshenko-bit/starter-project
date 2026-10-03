using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    public static void MapLab02Endpoints(this WebApplication app)
    {
        app.MapGet("/api/incidents/search", async (
            string? q,
            string? sortBy,
            SecureLabDbContext db,
            CancellationToken ct) =>
        {
            // Клієнт обирає лише одне з дозволених логічних значень.
            var sort = string.IsNullOrEmpty(sortBy)
                ? "createdAtUtc"
                : sortBy;

            if (sort is not ("createdAtUtc" or "severity" or "status"))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["sortBy"] =
                            ["Допустимі значення: createdAtUtc, severity, status."]
                    });
            }

            // Екрануємо спеціальні символи ILIKE.
            // Порядок важливий: спочатку сама зворотна скісна риска.
            var escaped = (q ?? "")
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

            var pattern = "%" + escaped + "%";

            var query = db.Incidents
                .AsNoTracking()
                .Where(incident =>
                    EF.Functions.ILike(incident.Title, pattern, "\\")
                    || EF.Functions.ILike(incident.Description, pattern, "\\"));

            // Enum зберігаються в БД як рядки.
            // Для предметного порядку задаємо явні ранги.
            var ordered = sort switch
            {
                "severity" => query.OrderBy(incident =>
                    incident.Severity == IncidentSeverity.Critical ? 0 :
                    incident.Severity == IncidentSeverity.High ? 1 :
                    incident.Severity == IncidentSeverity.Medium ? 2 : 3),

                "status" => query.OrderBy(incident =>
                    incident.Status == IncidentStatus.New ? 0 :
                    incident.Status == IncidentStatus.Triaged ? 1 :
                    incident.Status == IncidentStatus.InProgress ? 2 :
                    incident.Status == IncidentStatus.Resolved ? 3 : 4),

                _ => query.OrderByDescending(incident => incident.CreatedAtUtc)
            };

            var rows = await ordered
                .ThenBy(incident => incident.Id)
                .Take(50)
                .ToListAsync(ct);

            return Results.Ok(rows.Select(incident => new
            {
                incident.Id,
                incident.Title,
                incident.Description,
                Severity = incident.Severity.ToString(),
                Status = incident.Status.ToString(),
                incident.CreatedAtUtc
            }));
        });
        
        app.MapPost("/api/incidents", async (
            CreateIncidentRequest request,
            SecureLabDbContext db,
            CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var errors = new Dictionary<string, string[]>();

            // Нормалізуємо рядки один раз.
            var title = request.Title?.Trim() ?? "";
            var description = request.Description?.Trim() ?? "";

            // Required перевіряємо після Trim,
            // максимальну довжину — на початковому значенні.
            if (title.Length == 0)
            {
                errors["title"] = ["Назва інциденту обов’язкова."];
            }
            else if (request.Title!.Length > 160)
            {
                errors["title"] = ["Назва не може перевищувати 160 символів."];
            }

            if (description.Length == 0)
            {
                errors["description"] = ["Опис інциденту обов’язковий."];
            }
            else if (request.Description!.Length > 4000)
            {
                errors["description"] = ["Опис не може перевищувати 4000 символів."];
            }

            var severityIsValid =
                Enum.TryParse<IncidentSeverity>(
                    request.Severity,
                    ignoreCase: true,
                    out var severity)
                && Enum.IsDefined(severity);

            if (!severityIsValid)
            {
                errors["severity"] =
                    ["Допустимі значення: Low, Medium, High, Critical."];
            }

            if (request.OccurredAtUtc is null)
            {
                errors["occurredAtUtc"] = ["Дата і час інциденту обов’язкові."];
            }
            else if (request.OccurredAtUtc.Value > now.AddMinutes(5))
            {
                errors["occurredAtUtc"] =
                    ["Час інциденту не може бути більш ніж на 5 хвилин у майбутньому."];
            }

            // Для High/Critical потрібен змістовніший опис.
            if (severityIsValid
                && (severity == IncidentSeverity.High
                    || severity == IncidentSeverity.Critical)
                && description.Length < 40
                && !errors.ContainsKey("description"))
            {
                errors["description"] =
                    ["Для High або Critical опис має містити щонайменше 40 символів."];
            }

            // Некоректні дані не доходять до запитів до БД.
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            // Closed не блокує повторне створення.
            var duplicateExists = await db.Incidents.AnyAsync(
                incident =>
                    incident.Title == title
                    && (incident.Status == IncidentStatus.New
                        || incident.Status == IncidentStatus.Triaged
                        || incident.Status == IncidentStatus.InProgress
                        || incident.Status == IncidentStatus.Resolved),
                ct);

            if (duplicateExists)
            {
                return Results.Problem(
                    title: "Конфлікт створення інциденту",
                    detail: "Інцидент із такою назвою вже існує і ще не закритий.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var incident = new Incident
            {
                Id = Guid.NewGuid(),
                OwnerUserId = DbSeeder.AliceId,
                Title = title,
                Description = description,
                Severity = severity,
                Status = IncidentStatus.New,
                OccurredAtUtc = request.OccurredAtUtc!.Value.ToUniversalTime(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            db.Incidents.Add(incident);
            await db.SaveChangesAsync(ct);

            var response = new CreatedIncidentResponse(
                incident.Id,
                incident.Title,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc,
                incident.UpdatedAtUtc);

            return Results.Created($"/api/incidents/{incident.Id}", response);
        });
    }
}

public sealed record CreateIncidentRequest(
    string? Title, string? Description, string? Severity, DateTimeOffset? OccurredAtUtc);

public sealed record CreatedIncidentResponse(
    Guid Id,
    string Title,
    string Severity,
    string Status,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);