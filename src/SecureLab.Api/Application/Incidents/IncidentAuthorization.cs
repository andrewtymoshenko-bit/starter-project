using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Application.Incidents;

public static class IncidentOperations
{
    public static readonly OperationAuthorizationRequirement Read =
        new() { Name = nameof(Read) };

    public static readonly OperationAuthorizationRequirement UpdateContent =
        new() { Name = nameof(UpdateContent) };

    public static readonly OperationAuthorizationRequirement Delete =
        new() { Name = nameof(Delete) };

    public static readonly OperationAuthorizationRequirement Triage =
        new() { Name = nameof(Triage) };

    public static readonly OperationAuthorizationRequirement ViewInternalComment =
        new() { Name = nameof(ViewInternalComment) };
}

public sealed class IncidentAuthorizationHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, Incident>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        Incident incident)
    {
        var userIdValue = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Task.CompletedTask;
        }

        var isOwner = incident.OwnerUserId == userId;
        var isReporter = context.User.IsInRole(DbSeeder.ReporterRole);
        var isAnalyst = context.User.IsInRole(DbSeeder.AnalystRole);

        var allowed = requirement.Name switch
        {
            nameof(IncidentOperations.Read) =>
                isOwner || isAnalyst,

            nameof(IncidentOperations.UpdateContent) =>
                isOwner && isReporter,

            nameof(IncidentOperations.Delete) =>
                false,

            nameof(IncidentOperations.Triage) =>
                isAnalyst,

            nameof(IncidentOperations.ViewInternalComment) =>
                isAnalyst,

            _ => false
        };

        if (allowed)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}