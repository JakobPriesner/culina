using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Import;

/// <summary>
/// What a connection is, proved against a real database.
/// </summary>
/// <remarks>
/// <para>
/// No Tandoor instance is reachable from a test run, which turns out to be the
/// point rather than a limitation: connecting is supposed to fail when the app
/// on the other end does not answer, and these prove that it does — and that it
/// fails before anything is written down.
/// </para>
/// <para>
/// The rest is the part that must never regress: a token that goes in and does
/// not come back, and a connection that a stranger cannot see, use or browse.
/// </para>
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class RecipeSourceEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static readonly string[] OneRecipe = ["17"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Connect_ShouldStoreNothing_WhenTheOtherAppDoesNotAnswer()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        // Act
        // Nothing is listening, which is the same outcome a wrong address has.
        var response = await Connect(client, householdId, "https://tandoor.invalid");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var listed = await client.GetAsync($"/api/v1/recipe-sources?householdId={householdId}", Token);

        // The order is the feature: a connection stored first and tested later
        // looks fine here and fails the first time somebody tries to use it.
        Assert.Empty(listed.Json!.Value.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Connect_ShouldRefuse_AnAddressThatIsNotOne()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        // Act
        var response = await Connect(client, householdId, "file:///etc/passwd");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "import.invalid_source_address",
            response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Connect_ShouldRefuse_AnAppItCannotRead()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        // Act
        var response = await client.PostAsync(
            "/api/v1/recipe-sources",
            new
            {
                householdId,
                kind = "paprika",
                address = "https://recipes.example.com",
                token = "secret"
            },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "import.unknown_source_kind",
            response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Connect_ShouldRefuse_BothAWayInAtOnce()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        // Act
        var response = await client.PostAsync(
            "/api/v1/recipe-sources",
            new
            {
                householdId,
                kind = "tandoor",
                address = "https://tandoor.invalid",
                token = "tda_secret",
                username = "ada",
                password = "hunter2"
            },
            Token);

        // Assert
        // A request that says two things must not get a silent answer about
        // which of them was believed.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "import.ambiguous_credentials",
            response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Connect_ShouldRefuse_NeitherAWayIn()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        // Act
        var response = await client.PostAsync(
            "/api/v1/recipe-sources",
            new { householdId, kind = "tandoor", address = "https://tandoor.invalid" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Connect_ShouldRefuse_AHouseholdTheCallerIsNotIn()
    {
        // Arrange
        using var client = await SignedInAsync();

        using var stranger = await SecondAccountAsync();
        var theirHousehold = await OwnHouseholdAsync(stranger);

        // Act
        var response = await Connect(client, theirHousehold, "https://tandoor.invalid");

        // Assert
        // 404 rather than 403: a stranger learns nothing about which kitchens
        // exist, let alone what they have connected.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Browse_ShouldNotExist_ForAConnectionThatIsNotThisHouseholds()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var response = await client.GetAsync(
            $"/api/v1/recipe-sources/{Guid.NewGuid()}/recipes",
            Token);

        // Assert
        // Being able to use somebody else's connection would be being able to
        // read their Tandoor.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Import_ShouldRefuse_MoreRecipesThanOneImportCarries()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var response = await client.PostAsync(
            $"/api/v1/recipe-sources/{Guid.NewGuid()}/imports",
            new { externalIds = Enumerable.Range(1, 1001).Select(one => one.ToString()).ToArray() },
            Token);

        // Assert
        // Checked before the connection is even looked up: the ceiling bounds
        // the work one person can queue, so it has to be the first thing read.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "import.too_many_at_once",
            response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Import_ShouldRefuse_AnEmptySelection()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var response = await client.PostAsync(
            $"/api/v1/recipe-sources/{Guid.NewGuid()}/imports",
            new { externalIds = Array.Empty<string>() },
            Token);

        // Assert
        // An import of nothing would still make a cookbook to put nothing on.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "import.nothing_to_import",
            response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Import_ShouldNotStart_ForAConnectionThatIsNotThisHouseholds()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var response = await client.PostAsync(
            $"/api/v1/recipe-sources/{Guid.NewGuid()}/imports",
            new { externalIds = OneRecipe },
            Token);

        // Assert
        // Being able to import through somebody else's connection would be
        // being able to read their Tandoor.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Watch_ShouldNotExist_ForAnImportNobodyStarted()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var response = await client.GetAsync(
            $"/api/v1/recipe-sources/{Guid.NewGuid()}/imports/{Guid.NewGuid()}/events",
            Token);

        // Assert
        // A run that never was, one that has been forgotten, and one that is
        // somebody else's are one answer: a stranger learns nothing from the
        // difference, and neither does a stale tab.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Disconnect_ShouldBeNothingTheSecondTime()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var response = await client.DeleteAsync($"/api/v1/recipe-sources/{Guid.NewGuid()}", Token);

        // Assert
        // Disconnecting something already gone is the outcome the caller wanted.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Sources_ShouldNeedAHousehold_RatherThanGuessOne()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var response = await client.GetAsync("/api/v1/recipe-sources", Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Everything_ShouldRequireSigningIn()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var visitor = postgres.Api.NewApiClient();

        // Act
        var listed = await visitor.GetAsync($"/api/v1/recipe-sources?householdId={Guid.NewGuid()}", Token);

        // Assert
        // The endpoints that make the server fetch are the last place to allow
        // an anonymous caller.
        Assert.Equal(HttpStatusCode.Unauthorized, listed.StatusCode);
    }

    private static Task<ApiResponse> Connect(ApiClient client, Guid householdId, string address) =>
        client.PostAsync(
            "/api/v1/recipe-sources",
            new { householdId, kind = "tandoor", address, token = "tda_secret" },
            Token);

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

    private async Task<ApiClient> SecondAccountAsync()
    {
        // The first account is the instance's admin, so registration has to be
        // opened before a second one can exist.
        using var admin = postgres.Api.NewApiClient();

        await admin.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);
        await admin.PutAsync(
            "/api/v1/settings/registration",
            new { openRegistration = true, requireInvitation = false, maxUsers = 100 },
            Token);

        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "grace@example.com", displayName = "Grace", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "grace@example.com", password = Password },
            Token);

        return client;
    }

    private static async Task<Guid> HouseholdAsync(ApiClient client)
    {
        var me = await client.GetAsync("/api/v1/users/me", Token);

        return me.Json!.Value.GetProperty("households")[0].GetProperty("householdId").GetGuid();
    }

    private static async Task<Guid> OwnHouseholdAsync(ApiClient client)
    {
        var created = await client.PostAsync("/api/v1/households", new { name = "Ihre Küche" }, Token);

        return created.Json!.Value.GetProperty("householdId").GetGuid();
    }
}
