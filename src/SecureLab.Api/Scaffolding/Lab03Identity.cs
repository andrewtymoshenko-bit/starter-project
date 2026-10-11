using Microsoft.AspNetCore.Identity;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace SecureLab.Api.Scaffolding;

public static class Lab03Identity
{
    public static void AddLab03Identity(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
        }).AddEntityFrameworkStores<SecureLabDbContext>().AddDefaultTokenProviders();
        builder.Services.AddAuthorization();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
    }

    public static void MapLab03Identity(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (RegistrationInput input, UserManager<ApplicationUser> users) =>
        {
            // ЛР 03: завершіть DTO validation та перевірки довірчої межі.
            // Спочатку перевіряємо обов’язкові поля.
            if (string.IsNullOrWhiteSpace(input.UserName)
                || string.IsNullOrWhiteSpace(input.Email)
                || string.IsNullOrWhiteSpace(input.DisplayName)
                || string.IsNullOrEmpty(input.Password))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["registration"] =
                            ["Ім’я користувача, email, ім’я профілю та пароль обов’язкові."]
                    });
            }

            // Прибираємо крайні пробіли лише з текстових полів.
            // Пароль залишаємо без змін.
            var userName = input.UserName.Trim();
            var email = input.Email.Trim();
            var displayName = input.DisplayName.Trim();

            var errors = new Dictionary<string, string[]>();

            if (input.UserName.Length > 256
                || !userName.All(c =>
                    char.IsAsciiLetterOrDigit(c) || "-._@+".Contains(c)))
            {
                errors["userName"] =
                    ["Ім’я користувача: до 256 символів, латинські літери, цифри або -._@+."];
            }

            if (input.Email.Length > 256
                || email.Any(char.IsWhiteSpace)
                || !new EmailAddressAttribute().IsValid(email))
            {
                errors["email"] =
                    ["Потрібна коректна email-адреса довжиною до 256 символів."];
            }

            if (input.DisplayName.Length > 120
                || displayName.Any(char.IsControl))
            {
                errors["displayName"] =
                    ["Ім’я профілю: до 120 символів, без керівних символів."];
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                Email = email,
                DisplayName = displayName
            };

            var result = await users.CreateAsync(user, input.Password);
            if (!result.Succeeded)
            {
                var duplicate = result.Errors.Any(error =>
                    error.Code is "DuplicateUserName" or "DuplicateEmail");

                return Results.Problem(
                    title: duplicate
                        ? "Конфлікт реєстрації"
                        : "Реєстрацію відхилено",
                    detail: duplicate
                        ? "Облікові дані вже використовуються."
                        : "Дані не відповідають правилам реєстрації.",
                    statusCode: duplicate
                        ? StatusCodes.Status409Conflict
                        : StatusCodes.Status400BadRequest);
            }

            var role = await users.AddToRoleAsync(user, DbSeeder.ReporterRole);
            if (!role.Succeeded) throw new InvalidOperationException("Default role assignment failed.");

            var response = new RegistrationResponse(
                user.Id,
                user.UserName!,
                user.DisplayName);

            return Results.Created("/api/me", response);
        });

        app.MapPost("/api/auth/login", async (
            LoginInput input,
            SignInManager<ApplicationUser> signIn,
            HttpContext context,
            ILogger<Program> logger) =>
        {
            if (string.IsNullOrWhiteSpace(input.UserName)
                || string.IsNullOrEmpty(input.Password))
            {
                logger.LogInformation(
                    "AuthEvent={AuthEvent} Outcome={Outcome} TraceId={TraceId}",
                    "login",
                    "failed",
                    context.TraceIdentifier);

                return Results.Unauthorized();
            }

            var result = await signIn.PasswordSignInAsync(
                input.UserName,
                input.Password,
                isPersistent: false,
                lockoutOnFailure: false);

            logger.LogInformation(
                "AuthEvent={AuthEvent} Outcome={Outcome} TraceId={TraceId}",
                "login",
                result.Succeeded ? "success" : "failed",
                context.TraceIdentifier);

            return result.Succeeded
                ? Results.NoContent()
                : Results.Unauthorized();
        }).RequireRateLimiting("auth-login");

        app.MapPost("/api/auth/logout", async (
            SignInManager<ApplicationUser> signIn,
            HttpContext context,
            ILogger<Program> logger) =>
        {
            await signIn.SignOutAsync();

            logger.LogInformation(
                "AuthEvent={AuthEvent} Outcome={Outcome} TraceId={TraceId}",
                "logout",
                "success",
                context.TraceIdentifier);

            return Results.NoContent();
        }).RequireAuthorization();

        app.MapGet("/api/me", async (
            HttpContext context,
            SecureLabDbContext db) =>
        {
            var userIdClaim = context.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FindAsync(userId);

            if (user is null)
            {
                return Results.Unauthorized();
            }

            context.Response.Headers.CacheControl = "no-store";

            var response = new CurrentUserResponse(
                user.Id,
                user.UserName!,
                user.DisplayName);

            return Results.Ok(response);
        }).RequireAuthorization();
        // Приклад лише mechanics: не повертає identity і не реалізує оцінювану operation.
        app.MapGet("/api/auth/session-check", () => Results.NoContent()).RequireAuthorization();
    }
}

public sealed record RegistrationInput(string? UserName, string? Email, string? DisplayName, string? Password);
public sealed record LoginInput(string? UserName, string? Password);
public sealed record RegistrationResponse(
    Guid Id,
    string UserName,
    string DisplayName);
public sealed record CurrentUserResponse(
    Guid Id,
    string UserName,
    string DisplayName);