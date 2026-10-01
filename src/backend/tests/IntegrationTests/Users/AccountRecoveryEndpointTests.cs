using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Users;

/// <summary>
/// Changing a password, and getting back in after forgetting one — through
/// every way a recovery code can be wrong, because a recovery flow tested only
/// on its happy path is a second, unguarded sign-in form.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class AccountRecoveryEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";
    private const string NewPassword = "a sentence nobody else would pick";

    [Fact]
    public async Task ChangePassword_ShouldLetTheNewPasswordSignIn_AndNotTheOldOne()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var changed = await kitchen.Client.PutAsync(
            "/api/v1/users/me/password",
            new { currentPassword = Password, newPassword = NewPassword },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync("ada@example.com", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SignInAsync("ada@example.com", NewPassword)).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ShouldSignOutEveryOtherDevice_ButKeepThisOne()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        using var otherDevice = postgres.Api.NewApiClient();
        await otherDevice.PostAsync("/api/v1/sessions", Credentials("ada@example.com", Password), Token);

        // Act
        await kitchen.Client.PutAsync(
            "/api/v1/users/me/password",
            new { currentPassword = Password, newPassword = NewPassword },
            Token);

        // Assert
        // A password changed because somebody else knew it has to shut that
        // somebody out, without signing out the person who changed it.
        Assert.Equal(HttpStatusCode.OK, (await kitchen.Client.GetAsync("/api/v1/users/me", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/api/v1/users/me", Token)).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ShouldBeRefused_WhenTheCurrentPasswordIsWrong()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var changed = await kitchen.Client.PutAsync(
            "/api/v1/users/me/password",
            new { currentPassword = "not the password", newPassword = NewPassword },
            Token);

        // Assert
        // Not a 401: the caller is signed in, and an unauthorised answer would
        // sign them out for a typo.
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
        Assert.Equal("users.incorrect_password", changed.ProblemCode);
        Assert.Equal(HttpStatusCode.Created, (await SignInAsync("ada@example.com", Password)).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ShouldRefuseAShortPassword_AndKeepTheOldOne()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var changed = await kitchen.Client.PutAsync(
            "/api/v1/users/me/password",
            new { currentPassword = Password, newPassword = "short" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
        Assert.Equal("users.weak_password", changed.ProblemCode);
        Assert.Equal(HttpStatusCode.Created, (await SignInAsync("ada@example.com", Password)).StatusCode);
    }

    [Fact]
    public async Task RecoveryCodes_ShouldBeShownOnce_AndOnlyCountedAfterwards()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var before = await kitchen.Client.GetAsync("/api/v1/users/me/recovery-codes", Token);

        // Act
        var codes = await CreateCodesAsync(kitchen.Client);
        var after = await kitchen.Client.GetAsync("/api/v1/users/me/recovery-codes", Token);

        // Assert
        Assert.Equal(0, before.Json!.Value.GetProperty("remaining").GetInt32());
        Assert.Equal(10, codes.Count);
        Assert.Equal(10, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches("^[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}-[0-9A-Z]{4}$", code));
        Assert.Equal(10, after.Json!.Value.GetProperty("remaining").GetInt32());
        Assert.All(codes, code => Assert.DoesNotContain(code, after.Body, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecoveryCodes_ShouldNeedThePassword_SoAStolenSessionCannotKeepTheAccount()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var created = await kitchen.Client.PostAsync(
            "/api/v1/users/me/recovery-codes",
            new { password = "not the password" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Equal("users.incorrect_password", created.ProblemCode);
    }

    [Fact]
    public async Task Reset_ShouldSetANewPassword_AndSignOutEverySession()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var codes = await CreateCodesAsync(kitchen.Client);
        using var stranger = postgres.Api.NewApiClient();

        // Act
        var reset = await ResetAsync(stranger, "ada@example.com", codes[0], NewPassword);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await kitchen.Client.GetAsync("/api/v1/users/me", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync("ada@example.com", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SignInAsync("ada@example.com", NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Reset_ShouldUseUpTheCode_SoItWorksOnlyOnce()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var codes = await CreateCodesAsync(kitchen.Client);
        using var client = postgres.Api.NewApiClient();
        await ResetAsync(client, "ada@example.com", codes[0], NewPassword);

        // Act
        var reused = await ResetAsync(client, "ada@example.com", codes[0], "yet another long password");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        Assert.Equal("auth.invalid_recovery_code", reused.ProblemCode);
        Assert.Equal(HttpStatusCode.Created, (await SignInAsync("ada@example.com", NewPassword)).StatusCode);

        await SignInAsync(kitchen.Client, "ada@example.com", NewPassword);
        var left = await kitchen.Client.GetAsync("/api/v1/users/me/recovery-codes", Token);
        Assert.Equal(9, left.Json!.Value.GetProperty("remaining").GetInt32());
    }

    [Fact]
    public async Task Reset_ShouldRefuseAnotherAccountsCode_EvenThoughTheCodeIsReal()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        using var grace = await Kitchen.StrangerAsync(postgres);
        var gracesCodes = await CreateCodesAsync(grace);
        using var client = postgres.Api.NewApiClient();

        // Act
        var foreign = await ResetAsync(client, "ada@example.com", gracesCodes[0], NewPassword);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal("auth.invalid_recovery_code", foreign.ProblemCode);
        Assert.Equal(HttpStatusCode.OK, (await kitchen.Client.GetAsync("/api/v1/users/me", Token)).StatusCode);

        // Not spent by the failed attempt, either: Grace can still use it.
        var hers = await ResetAsync(client, "grace@example.com", gracesCodes[0], NewPassword);
        Assert.Equal(HttpStatusCode.NoContent, hers.StatusCode);
    }

    [Fact]
    public async Task Reset_ShouldAnswerIdentically_ForAnUnknownAddressAndAWrongCode()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await CreateCodesAsync(kitchen.Client);
        using var client = postgres.Api.NewApiClient();

        // Act
        var wrongCode = await ResetAsync(client, "ada@example.com", "AAAA-BBBB-CCCC-DDDD", NewPassword);
        var unknown = await ResetAsync(client, "nobody@example.com", "AAAA-BBBB-CCCC-DDDD", NewPassword);
        var notAnAddress = await ResetAsync(client, "nobody", "AAAA-BBBB-CCCC-DDDD", NewPassword);

        // Assert
        // Any difference tells whoever is guessing which addresses are
        // registered here.
        Assert.All(
            [wrongCode, unknown, notAnAddress],
            response =>
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal("auth.invalid_recovery_code", response.ProblemCode);
                Assert.Equal(
                    wrongCode.Json!.Value.GetProperty("detail").GetString(),
                    response.Json!.Value.GetProperty("detail").GetString());
            });
    }

    [Fact]
    public async Task Reset_ShouldAcceptTheCode_HoweverItWasTyped()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var codes = await CreateCodesAsync(kitchen.Client);
        using var client = postgres.Api.NewApiClient();

        // Act
        // Read off paper and typed on a phone: lower case, no dashes, spaces.
        var typed = codes[0].Replace("-", " ", StringComparison.Ordinal).ToLowerInvariant();
        var reset = await ResetAsync(client, "ada@example.com", typed, NewPassword);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
    }

    [Fact]
    public async Task Reset_ShouldRefuseAShortPassword_WithoutSpendingTheCode()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var codes = await CreateCodesAsync(kitchen.Client);
        using var client = postgres.Api.NewApiClient();

        // Act
        var weak = await ResetAsync(client, "ada@example.com", codes[0], "short");
        var retried = await ResetAsync(client, "ada@example.com", codes[0], NewPassword);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal("users.weak_password", weak.ProblemCode);
        Assert.Equal(HttpStatusCode.NoContent, retried.StatusCode);
    }

    [Fact]
    public async Task NewRecoveryCodes_ShouldStopTheOldSetWorking()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var old = await CreateCodesAsync(kitchen.Client);
        await CreateCodesAsync(kitchen.Client);
        using var client = postgres.Api.NewApiClient();

        // Act
        var reset = await ResetAsync(client, "ada@example.com", old[0], NewPassword);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        Assert.Equal("auth.invalid_recovery_code", reset.ProblemCode);
    }

    [Fact]
    public async Task IssuedCode_ShouldLetTheAdministratorHelpSomebodyBackIn()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        using var grace = await Kitchen.StrangerAsync(postgres);

        // Act
        var issued = await kitchen.Client.PostAsync("/api/v1/recovery-codes", new { email = "grace@example.com" }, Token);
        var code = issued.Json!.Value.GetProperty("code").GetString()!;

        using var client = postgres.Api.NewApiClient();
        var reset = await ResetAsync(client, "grace@example.com", code, NewPassword);

        // Assert
        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
        Assert.InRange(
            issued.Json!.Value.GetProperty("expiresAt").GetDateTimeOffset(),
            DateTimeOffset.UtcNow.AddHours(23),
            DateTimeOffset.UtcNow.AddHours(25));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await grace.GetAsync("/api/v1/users/me", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SignInAsync("grace@example.com", NewPassword)).StatusCode);
    }

    [Fact]
    public async Task IssuedCode_ShouldBeRefused_OnceItHasExpired()
    {
        // Arrange
        // Aged in the database because the API has no way to produce an
        // expired code, which is the point.
        var kitchen = await Kitchen.OpenAsync(postgres);
        using var grace = await Kitchen.StrangerAsync(postgres);
        var issued = await kitchen.Client.PostAsync("/api/v1/recovery-codes", new { email = "grace@example.com" }, Token);
        var code = issued.Json!.Value.GetProperty("code").GetString()!;

        await postgres.ExecuteAsync("update recovery_codes set expires_at = now() - interval '1 minute';", Token);

        // Act
        using var client = postgres.Api.NewApiClient();
        var reset = await ResetAsync(client, "grace@example.com", code, NewPassword);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        Assert.Equal("auth.invalid_recovery_code", reset.ProblemCode);
        Assert.Equal(HttpStatusCode.OK, (await grace.GetAsync("/api/v1/users/me", Token)).StatusCode);
    }

    [Fact]
    public async Task IssuedCode_ShouldBeForbidden_ForAnAccountThatIsNotTheAdministrator()
    {
        // Arrange
        await Kitchen.OpenAsync(postgres);
        using var grace = await Kitchen.StrangerAsync(postgres);

        // Act
        var issued = await grace.PostAsync("/api/v1/recovery-codes", new { email = "ada@example.com" }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, issued.StatusCode);
    }

    [Fact]
    public async Task IssuedCode_ShouldSaySo_WhenNoAccountHasTheAddress()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var issued = await kitchen.Client.PostAsync("/api/v1/recovery-codes", new { email = "nobody@example.com" }, Token);

        // Assert
        // The administrator can see every account anyway; a code made for
        // nobody would be a dead end they could not tell from a working one.
        Assert.Equal(HttpStatusCode.NotFound, issued.StatusCode);
    }

    [Fact]
    public async Task Reset_ShouldRefuseEvenTheRightCode_OnceTheAccountHasFailedTooOften()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:LoginPerAccountPerMinute"] = "2" });
        using var ada = factory.NewApiClient();
        await ada.PostAsync("/api/v1/users", new { email = "ada@example.com", displayName = "Ada", password = Password }, Token);
        await ada.PostAsync("/api/v1/sessions", Credentials("ada@example.com", Password), Token);
        var codes = await CreateCodesAsync(ada);
        using var client = factory.NewApiClient();

        // Act
        await ResetAsync(client, "ada@example.com", "AAAA-BBBB-CCCC-DDDD", NewPassword);
        await ResetAsync(client, "ada@example.com", "AAAA-BBBB-CCCC-DDDE", NewPassword);
        var right = await ResetAsync(client, "ada@example.com", codes[0], NewPassword);

        // Assert
        // The same per-account budget as signing in: spreading guesses across
        // addresses does not buy more of them.
        Assert.Equal(HttpStatusCode.BadRequest, right.StatusCode);
        Assert.Equal("auth.invalid_recovery_code", right.ProblemCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static object Credentials(string email, string password) => new { email, password };

    private async Task<ApiResponse> SignInAsync(string email, string password)
    {
        using var client = postgres.Api.NewApiClient();

        return await SignInAsync(client, email, password);
    }

    private static Task<ApiResponse> SignInAsync(ApiClient client, string email, string password) =>
        client.PostAsync("/api/v1/sessions", Credentials(email, password), Token);

    private static Task<ApiResponse> ResetAsync(ApiClient client, string email, string code, string password) =>
        client.PostAsync("/api/v1/password-resets", new { email, code, password }, Token);

    private static async Task<IReadOnlyList<string>> CreateCodesAsync(ApiClient client)
    {
        var created = await client.PostAsync("/api/v1/users/me/recovery-codes", new { password = Password }, Token);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        return [.. created.Json!.Value.GetProperty("codes").EnumerateArray().Select(code => code.GetString()!)];
    }
}
