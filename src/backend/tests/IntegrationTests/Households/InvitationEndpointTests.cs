using System.Net;
using Application.Abstractions.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Households;

[Collection(RequiresDatabase.Name)]
public class InvitationEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Invite_ShouldReturnTheCodeOnce_AndNeverAgain()
    {
        // Arrange
        var (owner, _) = await TwoUsersAsync();
        using var ownerClient = owner;
        var householdId = await FirstHouseholdIdAsync(owner);

        // Act
        var created = await owner.PostAsync(
            $"/api/v1/households/{householdId}/invitations",
            new { },
            Token);
        var listed = await owner.GetAsync($"/api/v1/households/{householdId}/invitations", Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotEmpty(created.Json!.Value.GetProperty("code").GetString()!);
        // Only the digest is stored, so listing cannot hand out a code.
        var summary = listed.Json!.Value.GetProperty("items")[0];
        Assert.False(summary.TryGetProperty("code", out _));
    }

    [Fact]
    public async Task Redeem_ShouldAddTheCallerToTheHousehold()
    {
        // Arrange
        var (owner, joiner) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var joinerClient = joiner;
        var householdId = await FirstHouseholdIdAsync(owner);
        var code = await IssueCodeAsync(owner, householdId);

        // Act
        var joined = await joiner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, joined.StatusCode);
        var members = await owner.GetAsync($"/api/v1/households/{householdId}/members", Token);
        Assert.Equal(2, members.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Redeem_ShouldWorkOnlyOnce_SoACodeCannotBeShared()
    {
        // Arrange
        var (owner, joiner) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var joinerClient = joiner;
        var householdId = await FirstHouseholdIdAsync(owner);
        var code = await IssueCodeAsync(owner, householdId);
        await joiner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);

        // Act
        var again = await joiner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);

        // Assert
        Assert.Equal("households.invitation_invalid", again.ProblemCode);
    }

    [Fact]
    public async Task Redeem_ShouldAnswerIdentically_ForUnknownAndUsedCodes()
    {
        // Arrange
        var (owner, joiner) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var joinerClient = joiner;
        var householdId = await FirstHouseholdIdAsync(owner);
        var code = await IssueCodeAsync(owner, householdId);
        await joiner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);

        // Act
        var used = await joiner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);
        var unknown = await joiner.PostAsync(
            "/api/v1/invitations/definitely-not-a-real-code/redemptions",
            new { },
            Token);

        // Assert
        // Distinguishing them would let someone probe codes.
        Assert.Equal(used.StatusCode, unknown.StatusCode);
        Assert.Equal(used.ProblemCode, unknown.ProblemCode);
        Assert.Equal(
            used.Json!.Value.GetProperty("detail").GetString(),
            unknown.Json!.Value.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Revoke_ShouldStopACodeWorking_Immediately()
    {
        // Arrange
        var (owner, joiner) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var joinerClient = joiner;
        var householdId = await FirstHouseholdIdAsync(owner);
        var created = await owner.PostAsync(
            $"/api/v1/households/{householdId}/invitations",
            new { },
            Token);
        var code = created.Json!.Value.GetProperty("code").GetString()!;
        var invitationId = created.Json!.Value.GetProperty("invitationId").GetGuid();

        // Act
        await owner.DeleteAsync(
            $"/api/v1/households/{householdId}/invitations/{invitationId}",
            Token);
        var redeemed = await joiner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);

        // Assert
        Assert.Equal("households.invitation_invalid", redeemed.ProblemCode);
    }

    [Fact]
    public async Task Invite_ShouldBeRefused_ForSomeoneWhoIsNotAMember()
    {
        // Arrange
        var (owner, stranger) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var strangerClient = stranger;
        var householdId = await FirstHouseholdIdAsync(owner);

        // Act
        var response = await stranger.PostAsync(
            $"/api/v1/households/{householdId}/invitations",
            new { },
            Token);

        // Assert
        // Not-found rather than forbidden, like every household read for a non-member.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Redeem_ShouldRecogniseAMember_AndLeaveTheCodeForSomebodyElse()
    {
        // Arrange
        // The owner opening their own link is not an error and must not use the code up.
        var (owner, joiner) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var joinerClient = joiner;
        var householdId = await FirstHouseholdIdAsync(owner);
        var code = await IssueCodeAsync(owner, householdId);

        // Act
        var opened = await owner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);
        var joined = await joiner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.True(opened.Json!.Value.GetProperty("alreadyMember").GetBoolean());
        Assert.Equal(householdId, opened.Json!.Value.GetProperty("householdId").GetGuid());

        Assert.Equal(HttpStatusCode.Created, joined.StatusCode);
        Assert.False(joined.Json!.Value.GetProperty("alreadyMember").GetBoolean());
    }

    [Fact]
    public async Task Redeem_ShouldStillSayInvalid_ToAMemberHoldingAUsedCode()
    {
        // Arrange
        // Recognising a member applies only to a working code.
        var (owner, joiner) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var joinerClient = joiner;
        var householdId = await FirstHouseholdIdAsync(owner);
        var code = await IssueCodeAsync(owner, householdId);
        await joiner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);

        // Act
        var again = await owner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal("households.invitation_invalid", again.ProblemCode);
    }

    [Fact]
    public async Task Redeem_ShouldLetSomebodyWithAHouseholdOfTheirOwnJoinAnother()
    {
        // Arrange
        var (owner, joiner) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var joinerClient = joiner;
        var householdId = await FirstHouseholdIdAsync(owner);
        var code = await IssueCodeAsync(owner, householdId);

        await joiner.PostAsync("/api/v1/households", new { name = "Grace's kitchen" }, Token);

        // Act
        var joined = await joiner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, joined.StatusCode);

        var theirs = await joiner.GetAsync("/api/v1/households", Token);

        Assert.Equal(2, theirs.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Read_ShouldNameTheHousehold_AndLeaveTheCodeUnused()
    {
        // Arrange
        var (owner, joiner) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var joinerClient = joiner;
        var householdId = await FirstHouseholdIdAsync(owner);
        var name = (await owner.GetAsync($"/api/v1/households/{householdId}", Token))
            .Json!.Value.GetProperty("name").GetString();
        var code = await IssueCodeAsync(owner, householdId);

        // Act
        var read = await joiner.GetAsync($"/api/v1/invitations/{code}", Token);

        // Assert
        // The name and nothing else.
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var property = Assert.Single(read.Json!.Value.EnumerateObject());
        Assert.Equal("householdName", property.Name);
        Assert.Equal(name, property.Value.GetString());

        // Reading is not joining, and the code still works for joining.
        var members = await owner.GetAsync($"/api/v1/households/{householdId}/members", Token);
        Assert.Equal(1, members.Json!.Value.GetProperty("items").GetArrayLength());
        var redeemed = await joiner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);
        Assert.Equal(HttpStatusCode.Created, redeemed.StatusCode);
    }

    [Fact]
    public async Task Read_ShouldAnswerIdentically_ForUnknownUsedExpiredAndBinnedCodes()
    {
        // Arrange
        var (owner, joiner) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var joinerClient = joiner;
        var householdId = await FirstHouseholdIdAsync(owner);

        var used = await IssueCodeAsync(owner, householdId);
        await joiner.PostAsync($"/api/v1/invitations/{used}/redemptions", new { }, Token);

        var expired = await IssueCodeAsync(owner, householdId);
        await postgres.ExecuteAsync(
            "update household_invitations set expires_at = now() - interval '1 day';",
            Token);

        var flat = (await owner.PostAsync("/api/v1/households", new { name = "Flat" }, Token))
            .Json!.Value.GetProperty("householdId").GetGuid();
        var binned = await IssueCodeAsync(owner, flat);
        await owner.DeleteCurrentAsync($"/api/v1/households/{flat}", Token);

        // Act
        var answers = new[]
        {
            await joiner.GetAsync("/api/v1/invitations/definitely-not-a-real-code", Token),
            await joiner.GetAsync($"/api/v1/invitations/{used}", Token),
            await joiner.GetAsync($"/api/v1/invitations/{expired}", Token),
            await joiner.GetAsync($"/api/v1/invitations/{binned}", Token)
        };

        // Assert
        // Distinguishing them would make reading a way to probe codes.
        Assert.All(answers, answer =>
        {
            Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
            Assert.Equal("households.invitation_invalid", answer.ProblemCode);
        });
        Assert.Single(answers.Select(answer => answer.Json!.Value.GetProperty("detail").GetString()).Distinct());
    }

    [Fact]
    public async Task Read_ShouldBeLimitedPerAddress_WithoutSpendingARedemption()
    {
        // Arrange
        // Own host, so the ceiling of two touches no other test.
        await postgres.ResetAsync(Token);
        using var factory = new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["RateLimits:InvitationPerIpPerHour"] = "2" });
        using var owner = factory.NewApiClient();
        await owner.PostAsync("/api/v1/users", new { email = "ada@example.com", displayName = "Ada", password = Password }, Token);
        await owner.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);
        var code = await IssueCodeAsync(owner, await FirstHouseholdIdAsync(owner));

        await owner.GetAsync($"/api/v1/invitations/{code}", Token);
        await owner.GetAsync($"/api/v1/invitations/{code}", Token);

        // Act
        var limited = await owner.GetAsync($"/api/v1/invitations/{code}", Token);
        var redeemed = await owner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);
    }

    [Fact]
    public async Task Read_ShouldNameNothing_ToSomebodySignedOut()
    {
        // Arrange
        // A code is a bearer token: the kitchen's name is shown only once signed in.
        var (owner, _) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var anonymous = postgres.Api.NewApiClient();
        var code = await IssueCodeAsync(owner, await FirstHouseholdIdAsync(owner));

        // Act
        var read = await anonymous.GetAsync($"/api/v1/invitations/{code}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<string> IssueCodeAsync(ApiClient owner, Guid householdId) =>
        (await owner.PostAsync($"/api/v1/households/{householdId}/invitations", new { }, Token))
            .Json!.Value.GetProperty("code").GetString()!;

    private static async Task<Guid> FirstHouseholdIdAsync(ApiClient client) =>
        (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

    [Fact]
    public async Task Redeem_ShouldRefuseACodeThatHasExpired()
    {
        // Arrange
        // Aged in the database: the API cannot produce an expired code.
        var (owner, other) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var otherClient = other;
        var householdId = await FirstHouseholdIdAsync(owner);

        var created = await owner.PostAsync(
            $"/api/v1/households/{householdId}/invitations",
            new { },
            Token);

        var code = created.Json!.Value.GetProperty("code").GetString()!;

        await postgres.ExecuteAsync(
            "update household_invitations set expires_at = now() - interval '1 day';",
            Token);

        // Act
        var redeemed = await other.PostAsync(
            $"/api/v1/invitations/{code}/redemptions",
            new { },
            Token);

        // Assert
        // Same answer as an unknown code, so guessers learn nothing.
        Assert.Equal(HttpStatusCode.NotFound, redeemed.StatusCode);
    }

    private async Task<ApiClient> SignedInAsync(string email)
    {
        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email, displayName = "Ada", password = Password },
            Token);
        await client.PostAsync("/api/v1/sessions", new { email, password = Password }, Token);

        return client;
    }

    private async Task<(ApiClient Owner, ApiClient Other)> TwoUsersAsync()
    {
        await postgres.ResetAsync(Token);

        var owner = await SignedInAsync("ada@example.com");

        var settings = postgres.Api.Services.GetRequiredService<RegistrationSettings>();
        settings.OpenRegistration = true;
        settings.RequireInvitation = false;

        return (owner, await SignedInAsync("grace@example.com"));
    }
}
