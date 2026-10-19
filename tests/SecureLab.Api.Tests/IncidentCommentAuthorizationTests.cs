using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Tests;

public sealed class IncidentCommentAuthorizationTests(
    SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private const string AlicePublicCommentId =
        "30000000-0000-0000-0000-000000000001";

    private const string BobPublicCommentId =
        "30000000-0000-0000-0000-000000000002";

    private const string AliceInternalCommentId =
        "30000000-0000-0000-0000-000000000003";

    private Task<HttpClient> LoginAsync(string userName)
    {
        var password =
            Environment.GetEnvironmentVariable("SeedUsers__Password");

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Задайте SeedUsers__Password перед запуском тестів.");
        }

        return factory.LoginAsync(userName, password);
    }

    // A-12: прямий identifier коментаря не обходить доступ до parent.
    [Theory]
    [InlineData(
        "alice", AlicePublicCommentId, HttpStatusCode.OK)]
    [InlineData(
        "alice", BobPublicCommentId, HttpStatusCode.NotFound)]
    [InlineData(
        "alice", AliceInternalCommentId, HttpStatusCode.NotFound)]
    [InlineData(
        "morgan", AliceInternalCommentId, HttpStatusCode.OK)]
    [InlineData(
        "morgan", BobPublicCommentId, HttpStatusCode.OK)]
    [InlineData(
        "admin", AliceInternalCommentId, HttpStatusCode.NotFound)]
    public async Task GetComment_EnforcesParentAndInternalVisibility(
        string userName,
        string commentId,
        HttpStatusCode expectedStatus)
    {
        using var client = await LoginAsync(userName);

        using var response = await client.GetAsync(
            $"/api/incident-comments/{commentId}");

        Assert.Equal(expectedStatus, response.StatusCode);

        if (expectedStatus == HttpStatusCode.OK)
        {
            var comment = await response.Content
                .ReadFromJsonAsync<IncidentCommentResponse>();

            Assert.NotNull(comment);
            Assert.Equal(Guid.Parse(commentId), comment.Id);
            Assert.False(string.IsNullOrWhiteSpace(comment.Text));
        }
        else
        {
            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            var body = document.RootElement;

            Assert.False(body.TryGetProperty("id", out _));
            Assert.False(body.TryGetProperty("text", out _));
            Assert.False(
                body.TryGetProperty("authorDisplayName", out _));
        }
    }

    // A-12: те саме internal-правило застосовано до details.
    [Fact]
    public async Task GetDetails_ShowsInternalCommentOnlyToAnalyst()
    {
        using var alice = await LoginAsync("alice");
        using var morgan = await LoginAsync("morgan");

        var url = $"/api/incidents/{DbSeeder.AliceIncidentId}";

        using var aliceResponse = await alice.GetAsync(url);
        using var morganResponse = await morgan.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, aliceResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, morganResponse.StatusCode);

        var aliceDetails = await aliceResponse.Content
            .ReadFromJsonAsync<IncidentDetailsResponse>();

        var morganDetails = await morganResponse.Content
            .ReadFromJsonAsync<IncidentDetailsResponse>();

        Assert.NotNull(aliceDetails);
        Assert.NotNull(morganDetails);

        var publicId = Guid.Parse(AlicePublicCommentId);
        var internalId = Guid.Parse(AliceInternalCommentId);

        Assert.Contains(
            aliceDetails.Comments,
            comment => comment.Id == publicId);

        Assert.DoesNotContain(
            aliceDetails.Comments,
            comment => comment.Id == internalId);

        Assert.Contains(
            morganDetails.Comments,
            comment => comment.Id == internalId);
    }

    // Політика 404 послідовна й на дочірньому маршруті.
    [Fact]
    public async Task GetComment_ForeignAndUnknown_ReturnEquivalent404()
    {
        using var alice = await LoginAsync("alice");

        using var foreign = await alice.GetAsync(
            $"/api/incident-comments/{BobPublicCommentId}");

        using var unknown = await alice.GetAsync(
            $"/api/incident-comments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        Assert.Equal(
            unknown.Content.Headers.ContentType?.ToString(),
            foreign.Content.Headers.ContentType?.ToString());

        var foreignBody = JsonNode.Parse(
            await foreign.Content.ReadAsStringAsync())!.AsObject();

        var unknownBody = JsonNode.Parse(
            await unknown.Content.ReadAsStringAsync())!.AsObject();

        foreignBody.Remove("traceId");
        unknownBody.Remove("traceId");

        Assert.True(
            JsonNode.DeepEquals(foreignBody, unknownBody),
            "Чужий і неіснуючий коментар мають різні відповіді 404.");
    }
}