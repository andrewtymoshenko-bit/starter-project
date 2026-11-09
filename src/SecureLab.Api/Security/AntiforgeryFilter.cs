using Microsoft.AspNetCore.Antiforgery;

namespace SecureLab.Api.Security;

public sealed class AntiforgeryFilter(
    IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Невалідний CSRF-токен",
                detail: "Оновіть токен і повторіть дію вручну.");
        }

        return await next(context);
    }
}