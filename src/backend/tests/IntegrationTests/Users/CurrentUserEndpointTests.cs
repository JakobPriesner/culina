using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
    public async Task Me_ShouldTagTwoAccountsDifferently_EvenAtTheSameVersion()
    {
        // Arrange
        // `/users/me` names a different person for every session, and both are
        // at version 1 on the day they sign up. A tag made of the version alone
        // would be identical for the two of them.
        await postgres.ResetAsync(Token);

        var first = await AccountAsync("ada@example.com", isFirst: true);
        var second = await AccountAsync("grace@example.com", isFirst: false);

        // Act
        var ada = await first.GetAsync("/api/v1/users/me", Token);
        var grace = await second.GetAsync("/api/v1/users/me", Token);

        // Assert
        Assert.NotNull(ada.ETag);
        Assert.NotEqual(ada.ETag, grace.ETag);

        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public async Task Me_ShouldNotAnswer304_WhenTheTagCameFromAnotherAccount()
    {
        // Arrange
        await postgres.ResetAsync(Token);

        var first = await AccountAsync("ada@example.com", isFirst: true);
        var second = await AccountAsync("grace@example.com", isFirst: false);
        var ada = await first.GetAsync("/api/v1/users/me", Token);

        // Act
        // Exactly what a browser does after two people sign in on one device:
        // it still holds the first response and offers its tag for revalidation.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(ada.ETag!));
        var response = await second.SendAsync(request, Token);

        // Assert
        // A 304 here would hand Ada's name and households to Grace.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("grace@example.com", response.Json!.Value.GetProperty("email").GetString());

        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public async Task Settings_ShouldStillAcceptItsOwnTagOnAWrite_WhenTheTagCarriesAnIdentity()
    {
        // Arrange
        using var client = await SignedInAsync();
        var before = await client.GetAsync("/api/v1/users/me/settings", Token);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/users/me/settings")
        {
            Content = JsonContent.Create(
                new { locale = "de", theme = "warm-paper", mode = "dark", measurementSystem = "metric" })
        };

        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(before.ETag!));

        var response = await client.SendAsync(request, Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Me_ShouldChangeItsTag_WhenTheAccountJoinsAHousehold()
    {
        // Arrange
        // Creating a household does not change the account, so a tag made only
        // of the account's version would answer 304 to a client that is
        // missing a kitchen it can now cook in.
        await postgres.ResetAsync(Token);

        using var admin = await AccountAsync("ada@example.com", isFirst: true);
        using var client = await AccountAsync("grace@example.com", isFirst: false);

        var before = await client.GetAsync("/api/v1/users/me", Token);

        // Act
        await client.PostAsync("/api/v1/households", new { name = "Grace's kitchen" }, Token);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(before.ETag!));
        var after = await client.SendAsync(request, Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Single(after.Json!.Value.GetProperty("households").EnumerateArray().ToList());
    }

    [Fact]
    public async Task Me_ShouldChangeItsTag_WhenAnAdministratorSwitchesTheAssistantOnOrOff()
    {
        // Arrange
        // Switching the assistant on changes nobody's account, so a tag made
        // of the account and its households answered 304 and the app kept an
        // all-false snapshot: the admin saved, saw "Ready", and New recipe
        // still offered no idea or photograph door — across reloads, because
        // the service worker keeps the same stale copy.
        using var client = await SignedInAsync();
        var before = await client.GetAsync("/api/v1/users/me", Token);

        // Act
        await client.PutAsync("/api/v1/settings/assistance", Assistant(enabled: true), Token);
        var switchedOn = await RevalidateAsync(client, before.ETag!);

        await client.PutAsync("/api/v1/settings/assistance", Assistant(enabled: false), Token);
        var switchedOff = await RevalidateAsync(client, switchedOn.ETag!);

        // Assert
        Assert.False(before.Json!.Value.GetProperty("assistance").GetProperty("draft").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, switchedOn.StatusCode);
        var on = switchedOn.Json!.Value.GetProperty("assistance");
        Assert.True(on.GetProperty("draft").GetBoolean());
        Assert.True(on.GetProperty("read").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, switchedOff.StatusCode);
        Assert.False(switchedOff.Json!.Value.GetProperty("assistance").GetProperty("draft").GetBoolean());
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
    public async Task Settings_ShouldChangeTheirTag_OnTheVeryFirstChange()
    {
        // Arrange
        // The first save is the one that used to be lost. Nothing is stored for
        // a new account, so the read is answered from the defaults — and while
        // those claimed the same version as the row the first save creates, the
        // tag did not move and the next read was a 304 carrying the values the
        // person had just replaced. It took a second change to show the first.
        using var client = await SignedInAsync();
        var before = await client.GetAsync("/api/v1/users/me/settings", Token);

        // Act
        await client.PutAsync(
            "/api/v1/users/me/settings",
            new { locale = "de", theme = "warm-paper", mode = "dark", measurementSystem = "imperial" },
            Token);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me/settings");

        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(before.ETag!));

        var after = await client.SendAsync(request, Token);

        // Assert
        // Not 304: what a conditional read must never do is answer "unchanged"
        // about something that just changed.
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal("de", after.Json!.Value.GetProperty("locale").GetString());
        Assert.Equal("imperial", after.Json!.Value.GetProperty("measurementSystem").GetString());
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

    /// <summary>One provider connected, and drafting and reading pointed at it.</summary>
    private static object Assistant(bool enabled) => new
    {
        enabled,
        connections = new[] { new { provider = "openai", apiKey = "sk-test", baseUrl = "" } },
        uses = new[]
        {
            new { capability = "draft", enabled = true, provider = "openai", model = "" },
            new { capability = "read", enabled = true, provider = "openai", model = "" }
        },
        monthlyBudget = (decimal?)null,
        personalBudget = (decimal?)null
    };

    private static async Task<ApiResponse> RevalidateAsync(ApiClient client, string etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");

        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));

        return await client.SendAsync(request, Token);
    }

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

    /// <summary>Registers and signs in one account, on an already-reset database.</summary>
    private async Task<ApiClient> AccountAsync(string email, bool isFirst)
    {
        var client = postgres.Api.NewApiClient();

        if (!isFirst)
        {
            // Only the first account may register on a closed instance, so the
            // second needs the policy opened first.
            using var admin = postgres.Api.NewApiClient();

            await admin.PostAsync(
                "/api/v1/sessions",
                new { email = "ada@example.com", password = Password },
                Token);
            await admin.PutAsync(
                "/api/v1/settings/registration",
                new { openRegistration = true, requireInvitation = false, maxUsers = 100 },
                Token);
        }

        await client.PostAsync(
            "/api/v1/users",
            new { email, displayName = email.Split('@')[0], password = Password },
            Token);
        await client.PostAsync("/api/v1/sessions", new { email, password = Password }, Token);

        return client;
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
