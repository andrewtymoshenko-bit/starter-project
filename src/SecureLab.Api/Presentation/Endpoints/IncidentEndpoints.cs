using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/incidents")
            .WithTags("Incidents")
            .RequireAuthorization();

        group.MapGet("/", GetListAsync)
            .WithName("GetIncidents")
            .Produces<IReadOnlyList<IncidentListItemResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetDetailsAsync)
            .WithName("GetIncidentDetails")
            .Produces<IncidentDetailsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateIncident")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteIncident")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/triage", TriageAsync)
            .WithName("TriageIncident")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/severity-summary", GetSeveritySummaryAsync)
            .WithName("GetIncidentSeveritySummary")
            .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

        endpoints.MapGet(
                "/api/incident-comments/{id:guid}",
                GetCommentAsync)
            .WithTags("Incident comments")
            .RequireAuthorization()
            .Produces<IncidentCommentResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetListAsync(
        string? status,
        HttpContext httpContext,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(httpContext, out var userId))
        {
            return Results.Challenge();
        }

        IncidentStatus? parsedStatus = null;

        if (status is not null)
        {
            if (!Enum.TryParse<IncidentStatus>(
                    status,
                    ignoreCase: true,
                    out var candidate)
                || !Enum.IsDefined(candidate))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["status"] =
                        [
                            "Допустимі значення: New, Triaged, InProgress, Resolved, Closed."
                        ]
                    });
            }

            parsedStatus = candidate;
        }

        var isAnalyst = httpContext.User.IsInRole(
            DbSeeder.AnalystRole);

        var incidents = await queries.GetListAsync(
            parsedStatus,
            userId,
            isAnalyst,
            cancellationToken);

        return Results.Ok(incidents);
    }

    private static async Task<IResult> GetDetailsAsync(
        Guid id,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        SecureLabDbContext dbContext,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var incident = await dbContext.Incidents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (incident is null)
        {
            return IncidentNotFound();
        }

        var canRead = await authorizationService.AuthorizeAsync(
            httpContext.User,
            incident,
            IncidentOperations.Read);

        if (!canRead.Succeeded)
        {
            return IncidentNotFound();
        }

        var canViewInternal = await authorizationService.AuthorizeAsync(
            httpContext.User,
            incident,
            IncidentOperations.ViewInternalComment);

        var response = await queries.GetDetailsAsync(
            id,
            canViewInternal.Succeeded,
            cancellationToken);

        return response is null
            ? IncidentNotFound()
            : Results.Ok(response);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateIncidentRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        SecureLabDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim();
        var description = request.Description?.Trim();

        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(title) || title.Length > 160)
        {
            errors["title"] =
            [
                "Заголовок обов’язковий, максимум 160 символів."
            ];
        }

        if (string.IsNullOrWhiteSpace(description)
            || description.Length > 4000)
        {
            errors["description"] =
            [
                "Опис обов’язковий, максимум 4000 символів."
            ];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var incident = await dbContext.Incidents.SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);

        if (incident is null)
        {
            return IncidentNotFound();
        }

        var canRead = await authorizationService.AuthorizeAsync(
            httpContext.User,
            incident,
            IncidentOperations.Read);

        if (!canRead.Succeeded)
        {
            return IncidentNotFound();
        }

        var canUpdate = await authorizationService.AuthorizeAsync(
            httpContext.User,
            incident,
            IncidentOperations.UpdateContent);

        if (!canUpdate.Succeeded)
        {
            return Results.Forbid();
        }

        incident.Title = title!;
        incident.Description = description!;
        incident.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        SecureLabDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var incident = await dbContext.Incidents.SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);

        if (incident is null)
        {
            return IncidentNotFound();
        }

        var canRead = await authorizationService.AuthorizeAsync(
            httpContext.User,
            incident,
            IncidentOperations.Read);

        if (!canRead.Succeeded)
        {
            return IncidentNotFound();
        }

        var canDelete = await authorizationService.AuthorizeAsync(
            httpContext.User,
            incident,
            IncidentOperations.Delete);

        return canDelete.Succeeded
            ? Results.NoContent()
            : Results.Forbid();
    }

    private static async Task<IResult> TriageAsync(
        Guid id,
        ChangeIncidentStatusRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        SecureLabDbContext dbContext,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(httpContext, out var userId))
        {
            return Results.Challenge();
        }

        var incident = await dbContext.Incidents.SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);

        if (incident is null)
        {
            return IncidentNotFound();
        }

        var canRead = await authorizationService.AuthorizeAsync(
            httpContext.User,
            incident,
            IncidentOperations.Read);

        if (!canRead.Succeeded)
        {
            return IncidentNotFound();
        }

        var canTriage = await authorizationService.AuthorizeAsync(
            httpContext.User,
            incident,
            IncidentOperations.Triage);

        if (!canTriage.Succeeded)
        {
            LogDeniedTriage(logger, httpContext, userId, incident.Id);
            return Results.Forbid();
        }

        if (incident.Status != IncidentStatus.New)
        {
            return Results.Problem(
                title: "Недопустимий перехід статусу",
                statusCode: StatusCodes.Status409Conflict);
        }

        var note = string.IsNullOrWhiteSpace(request.Note)
            ? "Первинну оцінку завершено."
            : request.Note.Trim();

        if (note.Length > 1000)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["note"] =
                    [
                        "Примітка не може перевищувати 1000 символів."
                    ]
                });
        }

        var now = DateTimeOffset.UtcNow;

        dbContext.IncidentStatusHistory.Add(
            new IncidentStatusHistory
            {
                Id = Guid.NewGuid(),
                IncidentId = incident.Id,
                ChangedByUserId = userId,
                OldStatus = incident.Status,
                NewStatus = IncidentStatus.Triaged,
                Note = note,
                CreatedAtUtc = now
            });

        incident.Status = IncidentStatus.Triaged;
        incident.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetCommentAsync(
        Guid id,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        SecureLabDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var comment = await dbContext.IncidentComments
            .AsNoTracking()
            .Include(item => item.Incident)
            .Include(item => item.Author)
            .SingleOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (comment is null)
        {
            return IncidentNotFound();
        }

        var canReadParent = await authorizationService.AuthorizeAsync(
            httpContext.User,
            comment.Incident,
            IncidentOperations.Read);

        if (!canReadParent.Succeeded)
        {
            return IncidentNotFound();
        }

        if (comment.IsInternal)
        {
            var canViewInternal =
                await authorizationService.AuthorizeAsync(
                    httpContext.User,
                    comment.Incident,
                    IncidentOperations.ViewInternalComment);

            if (!canViewInternal.Succeeded)
            {
                return IncidentNotFound();
            }
        }

        return Results.Ok(new IncidentCommentResponse(
            comment.Id,
            comment.Author.DisplayName,
            comment.Text,
            comment.CreatedAtUtc));
    }

    private static async Task<IResult> GetSeveritySummaryAsync(
        string? status,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        IncidentStatus? parsedStatus = null;

        if (status is not null)
        {
            if (!Enum.TryParse<IncidentStatus>(
                    status,
                    ignoreCase: true,
                    out var candidate)
                || !Enum.IsDefined(candidate))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["status"] =
                        [
                            "Допустимі значення: New, Triaged, InProgress, Resolved, Closed."
                        ]
                    });
            }

            parsedStatus = candidate;
        }

        return Results.Ok(await queries.GetSeveritySummaryAsync(
            parsedStatus,
            cancellationToken));
    }

    private static bool TryGetCurrentUserId(
        HttpContext httpContext,
        out Guid userId)
    {
        var userIdValue = httpContext.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out userId);
    }

    private static IResult IncidentNotFound() =>
        Results.Problem(
            title: "Інцидент не знайдено",
            statusCode: StatusCodes.Status404NotFound);

    private static void LogDeniedTriage(
        ILogger<Program> logger,
        HttpContext httpContext,
        Guid userId,
        Guid incidentId)
    {
        logger.LogWarning(
            "authorization.denied subject={SubjectId} action={Action} "
            + "resource={ResourceId} result={Result} "
            + "occurredAtUtc={OccurredAtUtc:O} traceId={TraceId}",
            userId,
            "incident.triage",
            incidentId,
            "denied",
            DateTimeOffset.UtcNow,
            Activity.Current?.Id ?? httpContext.TraceIdentifier);
    }
}