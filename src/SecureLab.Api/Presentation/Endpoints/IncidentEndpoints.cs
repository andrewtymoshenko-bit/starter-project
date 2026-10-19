using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using System.Security.Claims;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/incidents")
            .WithTags("Incidents");

        group.MapGet("/", GetListAsync)
            .WithName("GetIncidents")
            .Produces<IReadOnlyList<IncidentListItemResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetDetailsAsync)
            .WithName("GetIncidentDetails")
            .Produces<IncidentDetailsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/severity-summary", GetSeveritySummaryAsync)
            .WithName("GetIncidentSeveritySummary")
            .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>()
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization()
            .WithName("UpdateIncident")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/{id:guid}/triage", TriageAsync)
            .RequireAuthorization()
            .WithName("TriageIncident")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<IResult> GetListAsync(
        string? status,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        IncidentStatus? parsedStatus = null;
        if (status is not null)
        {
            if (!Enum.TryParse<IncidentStatus>(status, ignoreCase: true, out var candidate)
                || !Enum.IsDefined(candidate))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Допустимі значення: New, Triaged, InProgress, Resolved, Closed."]
                });
            }

            parsedStatus = candidate;
        }

        return Results.Ok(await queries.GetListAsync(parsedStatus, cancellationToken));
    }

    private static async Task<IResult> GetDetailsAsync(
        Guid id,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var incident = await queries.GetDetailsAsync(id, cancellationToken);
        return incident is null
            ? Results.Problem(
                title: "Інцидент не знайдено",
                detail: $"Інцидент '{id}' не існує.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(incident);
    }
    private static async Task<IResult> GetSeveritySummaryAsync(
        string? status,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        IncidentStatus? parsedStatus = null;
        if (status is not null)
        {
            if (!Enum.TryParse<IncidentStatus>(status, ignoreCase: true, out var candidate)
                || !Enum.IsDefined(candidate))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Допустимі значення: New, Triaged, InProgress, Resolved, Closed."]
                });
            }

            parsedStatus = candidate;
        }

        return Results.Ok(await queries.GetSeveritySummaryAsync(parsedStatus, cancellationToken));
    }
    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateIncidentRequest request,
        SecureLabDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim();
        var description = request.Description?.Trim();

        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(title) || title.Length > 160)
        {
            errors["title"] =
                ["Заголовок обов’язковий, максимум 160 символів."];
        }

        if (string.IsNullOrWhiteSpace(description)
            || description.Length > 4000)
        {
            errors["description"] =
                ["Опис обов’язковий, максимум 4000 символів."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // Навчальний vulnerable state:
        // користувач автентифікований, але право на ресурс не перевіряється.
        var incident = await dbContext.Incidents
            .SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (incident is null)
        {
            return Results.Problem(
                title: "Інцидент не знайдено",
                statusCode: StatusCodes.Status404NotFound);
        }

        incident.Title = title!;
        incident.Description = description!;
        incident.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
    private static async Task<IResult> TriageAsync(
        Guid id,
        ChangeIncidentStatusRequest request,
        HttpContext httpContext,
        SecureLabDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = httpContext.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Results.Unauthorized();
        }

        // Навчальний vulnerable state:
        // маршрут вимагає входу, але не перевіряє роль Analyst.
        var incident = await dbContext.Incidents.SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);

        if (incident is null)
        {
            return Results.Problem(
                title: "Інцидент не знайдено",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (incident.Status != IncidentStatus.New)
        {
            return Results.Problem(
                title: "Недопустимий перехід статусу",
                statusCode: StatusCodes.Status409Conflict);
        }

        var now = DateTimeOffset.UtcNow;

        dbContext.IncidentStatusHistory.Add(new IncidentStatusHistory
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            ChangedByUserId = userId,
            OldStatus = incident.Status,
            NewStatus = IncidentStatus.Triaged,
            Note = string.IsNullOrWhiteSpace(request.Note)
                ? "Первинну оцінку завершено."
                : request.Note.Trim(),
            CreatedAtUtc = now
        });

        incident.Status = IncidentStatus.Triaged;
        incident.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
}