using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Settings;

[Collection(RequiresDatabase.Name)]
public class RegistrationSettingsEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Read_ShouldShowTheShippedDefaults_OnAFreshInstance()
    {
        // Arrange
        using var admin = await AdminAsync();

        // Act
        var response = await admin.GetAsync("/api/v1/settings/registration", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Json!.Value.GetProperty("openRegistration").GetBoolean());
        Assert.True(response.Json!.Value.GetProperty("requireInvitation").GetBoolean());
    }

    [Fact]
    public async Task Update_ShouldTakeEffectOnTheNextRegistration_WithoutARestart()
    {
        // Arrange
        using var admin = await AdminAsync();

        // Act
        await admin.PutAsync(
            "/api/v1/settings/registration",
            new { openRegistration = true, requireInvitation = false, maxUsers = 100 },
            Token);

        using var newcomer = postgres.Api.NewApiClient();
        var registered = await newcomer.PostAsync(
            "/api/v1/users",
            new { email = "grace@example.com", displayName = "Grace", password = Password },
            Token);

        // Assert
        // The live singleton every handler holds is mutated in place, so there
        // is no cache to invalidate and no restart to wait for.
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
    }

    [Fact]
    public async Task Update_ShouldSurviveAReload_BecauseItIsPersistedNotJustMutated()
    {
        // Arrange
        using var admin = await AdminAsync();
        await admin.PutAsync(
            "/api/v1/settings/registration",
            new { openRegistration = true, requireInvitation = false, maxUsers = 42 },
            Token);

        // Act
        var read = await admin.GetAsync("/api/v1/settings/registration", Token);

        // Assert
        Assert.Equal(42, read.Json!.Value.GetProperty("maxUsers").GetInt32());
    }

    [Fact]
    public async Task Update_ShouldRejectAnImpossibleLimit_NamingTheField()
    {
        // Arrange
        using var admin = await AdminAsync();

        // Act
        var response = await admin.PutAsync(
            "/api/v1/settings/registration",
            new { openRegistration = true, requireInvitation = false, maxUsers = 0 },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("settings.invalid_value", response.ProblemCode);
    }

    [Fact]
    public async Task Settings_ShouldBeForbidden_ForAnAccountThatIsNotTheAdministrator()
    {
        // Arrange
        using var admin = await AdminAsync();
        await admin.PutAsync(
            "/api/v1/settings/registration",
            new { openRegistration = true, requireInvitation = false, maxUsers = 100 },
            Token);

        using var ordinary = postgres.Api.NewApiClient();
        await ordinary.PostAsync(
            "/api/v1/users",
            new { email = "grace@example.com", displayName = "Grace", password = Password },
            Token);
        await ordinary.PostAsync(
            "/api/v1/sessions",
            new { email = "grace@example.com", password = Password },
            Token);

        // Act
        var response = await ordinary.GetAsync("/api/v1/settings/registration", Token);

        // Assert
        // Checked against the users table on every request rather than carried
        // as a claim, so it cannot go stale.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Registration_ShouldAcceptAnInvitationCode_WhenTheInstanceRequiresOne()
    {
        // Arrange
        using var admin = await AdminAsync();
        await admin.PutAsync(
            "/api/v1/settings/registration",
            new { openRegistration = true, requireInvitation = true, maxUsers = 100 },
            Token);

        var householdId = (await admin.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();
        var code = (await admin.PostAsync(
                $"/api/v1/households/{householdId}/invitations",
                new { },
                Token))
            .Json!.Value.GetProperty("code").GetString();

        using var newcomer = postgres.Api.NewApiClient();

        // Act
        var registered = await newcomer.PostAsync(
            "/api/v1/users",
            new
            {
                email = "grace@example.com",
                displayName = "Grace",
                password = Password,
                invitationCode = code
            },
            Token);

        // Assert
        // The account and its membership are one atomic step: a consumed
        // invitation with no account behind it would be worse than a refusal.
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        Assert.Equal(householdId, registered.Json!.Value.GetProperty("householdId").GetGuid());

        var members = await admin.GetAsync($"/api/v1/households/{householdId}/members", Token);
        Assert.Equal(2, members.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Registration_ShouldBeRefused_WhenACodeIsRequiredAndNoneIsGiven()
    {
        // Arrange
        using var admin = await AdminAsync();
        await admin.PutAsync(
            "/api/v1/settings/registration",
            new { openRegistration = true, requireInvitation = true, maxUsers = 100 },
            Token);

        using var newcomer = postgres.Api.NewApiClient();

        // Act
        var registered = await newcomer.PostAsync(
            "/api/v1/users",
            new { email = "grace@example.com", displayName = "Grace", password = Password },
            Token);

        // Assert
        Assert.Equal("households.invitation_invalid", registered.ProblemCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ApiClient> AdminAsync()
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
