using System.Net.Http.Json;

namespace SecureLab.Api.Tests;

public static class AntiforgeryTestExtensions
{
    private sealed record TokenResponse(string Token);

    public static async Task UseAntiforgeryTokenAsync(
        this HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        using var response =
            await client.GetAsync("/api/auth/csrf-token");

        response.EnsureSuccessStatusCode();

        var body =
            await response.Content.ReadFromJsonAsync<TokenResponse>();

        if (string.IsNullOrWhiteSpace(body?.Token))
        {
            throw new InvalidOperationException(
                "Antiforgery request token is missing.");
        }

        client.DefaultRequestHeaders.Add(
            "X-CSRF-TOKEN", body.Token);
    }
}