using SecureLab.Api.Scaffolding;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Endpoints;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "Client"
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("SecureLab")
    ?? throw new InvalidOperationException(
        "Connection string 'SecureLab' не налаштовано.");

builder.Services.AddDbContext<SecureLabDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<IncidentQueries>();

builder.Services.AddScoped<IAuthorizationHandler, IncidentAuthorizationHandler>();

builder.AddLab03Identity();


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth-login", context =>
    {
        var configuration = context.RequestServices
            .GetRequiredService<IConfiguration>();

        var permitLimit =
            configuration.GetValue<int?>("LoginRateLimit:PermitLimit")
            ?? 10;

        var windowSeconds =
            configuration.GetValue<int?>("LoginRateLimit:WindowSeconds")
            ?? 60;

        if (permitLimit <= 0 || windowSeconds <= 0)
        {
            throw new InvalidOperationException(
                "Параметри обмеження входу мають бути додатними.");
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<Program>>();

        logger.LogInformation(
            "AuthEvent={AuthEvent} Outcome={Outcome} TraceId={TraceId}",
            "login",
            "rate_limited",
            context.HttpContext.TraceIdentifier);

        await Results.Problem(
            title: "Забагато спроб входу",
            detail: "Спробуйте повторити вхід пізніше.",
            statusCode: StatusCodes.Status429TooManyRequests)
            .ExecuteAsync(context.HttpContext);
    };
});

var app = builder.Build();

var resetRequested = args.Contains("--reset-database", StringComparer.Ordinal);
await DatabaseBootstrap.InitializeAsync(app.Services, app.Configuration, app.Environment, resetRequested);

if (resetRequested)
{
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    await next(context);
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.UseDefaultFiles();
app.UseStaticFiles();




app.MapGet("/health", async (SecureLabDbContext dbContext, CancellationToken cancellationToken) =>
    await dbContext.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.Problem(
            title: "База даних недоступна",
            statusCode: StatusCodes.Status503ServiceUnavailable))
    .WithName("GetHealth")
    .Produces(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapIncidentEndpoints();
app.MapLab03Identity();
app.MapLab02Endpoints();

app.MapFallback("/api/{**path}", () => Results.Problem(
    title: "Маршрут не знайдено",
    detail: "Такого маршрутту не існує",
    statusCode: StatusCodes.Status400BadRequest));
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
