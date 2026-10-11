using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SecureLab.Api.Tests;

public sealed class LoginRateLimitTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task Login_ExceedingLimit_Returns429_ThenRecovers()
    {
        using var limitedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["LoginRateLimit:PermitLimit"] = "2",
                        ["LoginRateLimit:WindowSeconds"] = "3"
                    });
            });
        });

        using var client = limitedFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });

        var configuration = limitedFactory.Services
            .GetRequiredService<IConfiguration>();

        Assert.Equal(
            2,
            configuration.GetValue<int>("LoginRateLimit:PermitLimit"));

        Assert.Equal(
            3,
            configuration.GetValue<int>("LoginRateLimit:WindowSeconds"));

        Task<HttpResponseMessage> SendLoginAsync()
        {
            return client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    userName = "missing-" + Guid.NewGuid().ToString("N"),
                    password = "Local-probe-only!123"
                });
        }

        using var first = await SendLoginAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);

        using var second = await SendLoginAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);

        client.DefaultRequestHeaders.Add(
            "X-Forwarded-For",
            "198.51.100.25");

        using var rejected = await SendLoginAsync();

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            rejected.StatusCode);

        Assert.Null(rejected.Headers.Location);

        Assert.Equal(
            "application/problem+json",
            rejected.Content.Headers.ContentType?.MediaType);

        using var error = JsonDocument.Parse(
            await rejected.Content.ReadAsStringAsync());

        Assert.Equal(
            429,
            error.RootElement.GetProperty("status").GetInt32());

        Assert.Equal(
            "Забагато спроб входу",
            error.RootElement.GetProperty("title").GetString());

        using var profile = await client.GetAsync("/api/me");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            profile.StatusCode);

        await Task.Delay(TimeSpan.FromSeconds(4));

        using var recovered = await SendLoginAsync();

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            recovered.StatusCode);

        Assert.Null(recovered.Headers.Location);
    }
}