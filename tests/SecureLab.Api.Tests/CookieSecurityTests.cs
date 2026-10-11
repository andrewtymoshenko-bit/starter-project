using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureLab.Api.Tests;

public sealed class CookieSecurityTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task LoginOverHttps_SetsRequiredCookieFlags()
    {
        var password =
            Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Задайте SeedUsers__Password перед запуском тестів.");

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                userName = "alice",
                password
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var cookieAttributes = response.Headers
            .GetValues("Set-Cookie")
            .Where(header => header.StartsWith(
                ".AspNetCore.Identity.Application=",
                StringComparison.Ordinal))
            .Select(header => header
                .Split(';')
                .Skip(1)
                .Select(attribute => attribute.Trim().ToLowerInvariant())
                .ToArray())
            .ToArray();

        var attributes = Assert.Single(cookieAttributes);

        Assert.Contains("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=lax", attributes);
        Assert.Contains("path=/", attributes);

        Assert.DoesNotContain(
            attributes,
            attribute => attribute.StartsWith("expires="));

        Assert.DoesNotContain(
            attributes,
            attribute => attribute.StartsWith("max-age="));
    }
}