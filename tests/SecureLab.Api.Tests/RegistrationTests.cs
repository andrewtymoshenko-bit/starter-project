using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Tests;

public sealed class RegistrationTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    [Theory]
    [InlineData(false)] // Повторне ім’я користувача.
    [InlineData(true)]  // Повторний email.
    public async Task Register_DuplicateNameOrEmail_Returns409(
        bool duplicateEmail)
    {
        var password =
            Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Задайте SeedUsers__Password перед запуском тестів.");

        var userName = "reg" + Guid.NewGuid().ToString("N");
        var otherName = userName + "other";
        var email = userName + "@example.test";

        using var client = factory.CreateClient();

        try
        {
            // Перша реєстрація має бути успішною.
            using var first = await client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    userName,
                    email,
                    displayName = "Навчальний користувач",
                    password
                });

            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal(
                "application/json",
                first.Content.Headers.ContentType?.MediaType);

            using var firstBody = JsonDocument.Parse(
                await first.Content.ReadAsStringAsync());

            // Дозволено повернути лише ці три поля.
            var fields = firstBody.RootElement
                .EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.Equal(
                new[] { "displayName", "id", "userName" },
                fields);

            Assert.Equal(
                userName,
                firstBody.RootElement.GetProperty("userName").GetString());

            // Змінюємо одне поле, щоб перевіряти причини окремо.
            using var duplicate = await client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    userName = duplicateEmail ? otherName : userName,
                    email = duplicateEmail
                        ? email
                        : otherName + "@example.test",
                    displayName = "Навчальний користувач",
                    password
                });

            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            Assert.Equal(
                "application/problem+json",
                duplicate.Content.Headers.ContentType?.MediaType);

            using var duplicateBody = JsonDocument.Parse(
                await duplicate.Content.ReadAsStringAsync());

            Assert.Equal(
                409,
                duplicateBody.RootElement.GetProperty("status").GetInt32());

            Assert.Equal(
                "Конфлікт реєстрації",
                duplicateBody.RootElement.GetProperty("title").GetString());

            // Відхилений запит з новим ім’ям не створив користувача.
            await using var scope = factory.Services.CreateAsyncScope();
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            Assert.Null(await users.FindByNameAsync(otherName));
        }
        finally
        {
            // Прибираємо тестові акаунти навіть після невдалої перевірки.
            await DeleteUserAsync(userName);
            await DeleteUserAsync(otherName);
        }
    }

    private async Task DeleteUserAsync(string userName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.FindByNameAsync(userName);

        if (user is not null)
        {
            var result = await users.DeleteAsync(user);
            Assert.True(result.Succeeded);
        }
    }
}