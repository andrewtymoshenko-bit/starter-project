using SecureLab.Api.Scaffolding;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Endpoints;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "Client"
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "SecureLab.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";

    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

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
    options.AddPolicy("incident-search", context =>
{
    var configuration = context.RequestServices
        .GetRequiredService<IConfiguration>();

    var permitLimit =
        configuration.GetValue<int?>("SearchRateLimit:PermitLimit")
        ?? 30;

    var windowSeconds =
        configuration.GetValue<int?>("SearchRateLimit:WindowSeconds")
        ?? 60;

    if (permitLimit <= 0 || windowSeconds <= 0)
    {
        throw new InvalidOperationException(
            "Параметри обмеження пошуку мають бути додатними.");
    }

    var userId = context.User.FindFirstValue(
        ClaimTypes.NameIdentifier);

    var partitionKey =
        context.User.Identity?.IsAuthenticated == true
        && !string.IsNullOrWhiteSpace(userId)
            ? "user:" + userId
            : "ip:" + (
                context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown");

    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
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


        if (context.HttpContext.Request.Path == "/api/incidents/search")
        {
            logger.LogInformation(
                "SecurityEvent={SecurityEvent} Outcome={Outcome} TraceId={TraceId}",
                "search",
                "rate_limited",
                context.HttpContext.TraceIdentifier);

            await Results.Problem(
                title: "Забагато пошукових запитів",
                detail: "Спробуйте повторити пошук пізніше.",
                statusCode: StatusCodes.Status429TooManyRequests)
                .ExecuteAsync(context.HttpContext);

            return;
        }

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

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Lab05BrowserProbe", policy =>
        {
            policy.WithOrigins("http://localhost:4173")
                  .WithMethods("GET")
                  .WithHeaders("X-Lab-Cors-Probe")
                  .AllowCredentials();
        });
    });
}

var app = builder.Build();

var resetRequested = args.Contains("--reset-database", StringComparer.Ordinal);
await DatabaseBootstrap.InitializeAsync(app.Services, app.Configuration, app.Environment, resetRequested);

if (resetRequested)
{
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();

var csp =
    "default-src 'self'; "
    + "script-src 'self'; "
    + "style-src 'self'; "
    + "connect-src 'self'; "
    + "img-src 'self'; "
    + "frame-ancestors 'none'";

app.Use(async (context, next) =>
{
    var isDevelopmentApiReference =
        app.Environment.IsDevelopment()
        && context.Request.Path.StartsWithSegments("/scalar");

    context.Response.OnStarting(() =>
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";

        if (!isDevelopmentApiReference)
        {
            context.Response.Headers["Content-Security-Policy"] = csp;
        }

        return Task.CompletedTask;
    });

    await next(context);
});

app.UseExceptionHandler();
app.UseStatusCodePages();


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseCors("Lab05BrowserProbe");
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
app.MapGet("/api/auth/csrf-token", (
    HttpContext context,
    IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);

    return Results.Ok(new
    {
        token = tokens.RequestToken
    });
})
.RequireAuthorization();

app.MapLab02Endpoints();

app.MapFallback("/api/{**path}", () => Results.Problem(
    title: "Маршрут не знайдено",
    detail: "Такого маршрутту не існує",
    statusCode: StatusCodes.Status400BadRequest));
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
