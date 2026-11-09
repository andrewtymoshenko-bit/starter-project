using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SecureLab.Api.Tests;

public sealed class CookieSecurityTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task LoginOverHttps_SetsRequiredCookieFlags(
    string environment)
    {
        var password =
            Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException(
                "Задайте SeedUsers__Password перед запуском тестів.");

        using var configuredFactory = factory.WithWebHostBuilder(builder =>
{
    builder.UseEnvironment(environment);

    builder.UseSetting(
        "ConnectionStrings:SecureLab",
        "Host=127.0.0.1;Port=54329;Database=securelab;"
        + "Username=securelab;Password=local-study-password;"
        + "Include Error Detail=false");
});

        Assert.Equal(
            environment,
            configuredFactory.Services
                .GetRequiredService<IHostEnvironment>()
                .EnvironmentName);

        using var client = configuredFactory.CreateClient(
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

        using var tokenResponse =
    await client.GetAsync("/api/auth/csrf-token");

        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        var antiforgeryCookie = Assert.Single(
            tokenResponse.Headers.GetValues("Set-Cookie"),
            header => header.StartsWith(
                "SecureLab.Antiforgery=",
                StringComparison.Ordinal));

        var antiforgeryAttributes = antiforgeryCookie
            .Split(';')
            .Skip(1)
            .Select(attribute => attribute.Trim().ToLowerInvariant())
            .ToArray();

        Assert.Contains("httponly", antiforgeryAttributes);
        Assert.Contains("secure", antiforgeryAttributes);
        Assert.Contains("samesite=strict", antiforgeryAttributes);
        Assert.Contains("path=/", antiforgeryAttributes);

        using var validation = await client.GetAsync(
    "/api/incidents?status=Unknown");

        Assert.Equal(HttpStatusCode.BadRequest, validation.StatusCode);
        Assert.Equal(
            "application/problem+json",
            validation.Content.Headers.ContentType?.MediaType);

        using var missing = await client.GetAsync(
            "/api/incidents/99999999-9999-9999-9999-999999999999");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(
            "application/problem+json",
            missing.Content.Headers.ContentType?.MediaType);

    }
}