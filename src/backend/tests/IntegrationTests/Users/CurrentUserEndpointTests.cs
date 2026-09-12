using System.Net;
using System.Net.Http.Headers;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Users;

[Collection(RequiresDatabase.Name)]
public class CurrentUserEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Me_ShouldReturnTheUserWithTheirHouseholds_SoBootNeedsOneRequest()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var response = await client.GetAsync("/api/v1/users/me", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = response.Json!.Value;
        Assert.Equal("ada@example.com", me.GetProperty("email").GetString());
        Assert.True(me.GetProperty("isAdmin").GetBoolean());
        var household = Assert.Single(me.GetProperty("households").EnumerateArray().ToList());
        Assert.Equal("owner", household.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Me_ShouldCarryAnETag_AndAnswer304_WhenNothingChanged()
    {
        // Arrange
        using var client = await SignedInAsync();
        var first = await client.GetAsync("/api/v1/users/me", Token);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(first.ETag!));
        var second = await client.SendAsync(request, Token);

        // Assert
        Assert.NotNull(first.ETag);
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Empty(second.Body);
    }

    [Fact]
    public async Task Me_ShouldBeUnauthorised_WhenThereIsNoSession()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();

        // Act
        var response = await client.GetAsync("/api/v1/users/me", Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.not_authenticated", response.ProblemCode);
    }

    [Fact]
    public async Task Rename_ShouldRequireIfMatch_SoAStaleClientCannotClobber()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var response = await client.PatchAsync(
            "/api/v1/users/me",
            new { displayName = "Ada Lovelace" },
            Token);

        // Assert
        // 428, not 412: the client has not sent a stale version, it has sent
        // none, and the fix is to read the resource first.
        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
        Assert.Equal("request.precondition_required", response.ProblemCode);
    }

    [Fact]
    public async Task Rename_ShouldSucceedAndBumpTheVersion_WhenIfMatchIsCurrent()
    {
        // Arrange
        using var client = await SignedInAsync();
        var before = await client.GetAsync("/api/v1/users/me", Token);

        // Act
        var response = await PatchWithMatchAsync(client, before.ETag!, "Ada Lovelace");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Ada Lovelace", response.Json!.Value.GetProperty("displayName").GetString());
        Assert.Equal(2, response.Json!.Value.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Rename_ShouldFailThePrecondition_WhenTheVersionHasMovedOn()
    {
        // Arrange
        using var client = await SignedInAsync();
        var before = await client.GetAsync("/api/v1/users/me", Token);
        await PatchWithMatchAsync(client, before.ETag!, "Ada Lovelace");

        // Act
        var stale = await PatchWithMatchAsync(client, before.ETag!, "Someone Else");

        // Assert
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("request.version_mismatch", stale.ProblemCode);
    }

    [Fact]
    public async Task Settings_ShouldStartAtTheDefaults_ForAnAccountThatNeverChangedThem()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var response = await client.GetAsync("/api/v1/users/me/settings", Token);

        // Assert
        var settings = response.Json!.Value;
        Assert.Equal("en", settings.GetProperty("locale").GetString());
        Assert.Equal("warm-paper", settings.GetProperty("theme").GetString());
        Assert.Equal("system", settings.GetProperty("mode").GetString());
        Assert.Equal("metric", settings.GetProperty("measurementSystem").GetString());
    }

    [Fact]
    public async Task Settings_ShouldPersistAChange_AndReadBackTheSameValues()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var saved = await client.PutAsync(
            "/api/v1/users/me/settings",
            new { locale = "de", theme = "warm-paper", mode = "dark", measurementSystem = "metric" },
            Token);
        var read = await client.GetAsync("/api/v1/users/me/settings", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("de", read.Json!.Value.GetProperty("locale").GetString());
        Assert.Equal("dark", read.Json!.Value.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Settings_ShouldNameEveryBadValue_SoTheFormCanMarkTheRightControl()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var response = await client.PutAsync(
            "/api/v1/users/me/settings",
            new { locale = "klingon", theme = "warm-paper", mode = "plaid", measurementSystem = "cubits" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = response.Json!.Value.GetProperty("errors").EnumerateArray()
            .Select(cause => cause.GetProperty("field").GetString())
            .ToList();
        Assert.Equal(["locale", "mode", "measurementSystem"], fields);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ApiResponse> PatchWithMatchAsync(
        ApiClient client,
        string etag,
        string displayName)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/users/me")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { displayName })
        };

        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));

        return await client.SendAsync(request, Token);
    }

    private async Task<ApiClient> SignedInAsync()
    {
        await postgres.ResetAsync(Token);

        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);

        return client;
    }
}
