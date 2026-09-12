using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Abstractions.Settings;
using Infrastructure.Persistence;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Households;

[Collection(RequiresDatabase.Name)]
public class HouseholdEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Create_ShouldMakeTheCallerAnOwner_SoTheHouseholdIsAdministrable()
    {
        // Arrange
        using var owner = await FreshOwnerAsync();

        // Act
        var response = await owner.PostAsync("/api/v1/households", new { name = "Cabin" }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("owner", response.Json!.Value.GetProperty("yourRole").GetString());
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task GetAll_ShouldListTheFirstHouseholdAndAnyOthers()
    {
        // Arrange
        using var owner = await FreshOwnerAsync();
        await owner.PostAsync("/api/v1/households", new { name = "Cabin" }, Token);

        // Act
        var response = await owner.GetAsync("/api/v1/households", Token);

        // Assert
        // One created with the first account, one created just now.
        Assert.Equal(2, response.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetById_ShouldReturn404ForANonMember_NotAForbidden()
    {
        // Arrange
        var (owner, stranger) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);

        // Act
        var response = await stranger.GetAsync($"/api/v1/households/{householdId}", Token);

        // Assert
        // Answering "forbidden" would confirm the household exists, which is
        // exactly what a non-member has no business learning.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("households.not_found", response.ProblemCode);
        owner.Dispose();
        stranger.Dispose();
    }

    [Fact]
    public async Task Rename_ShouldBeRefusedForAPlainMember_EvenThoughTheyCanSeeIt()
    {
        // Arrange
        var (owner, member) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        await AddMemberAsync(householdId, member);

        var current = await member.GetAsync($"/api/v1/households/{householdId}", Token);

        // Act
        var response = await PatchAsync(member, $"/api/v1/households/{householdId}", current.ETag!, new { name = "Mine now" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("households.not_owner", response.ProblemCode);
        owner.Dispose();
        member.Dispose();
    }

    [Fact]
    public async Task Rename_ShouldSucceedForAnOwner_WithACurrentIfMatch()
    {
        // Arrange
        using var owner = await FreshOwnerAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        var current = await owner.GetAsync($"/api/v1/households/{householdId}", Token);

        // Act
        var response = await PatchAsync(owner, $"/api/v1/households/{householdId}", current.ETag!, new { name = "The Kitchen" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("The Kitchen", response.Json!.Value.GetProperty("name").GetString());
    }

    [Fact]
    public async Task RemoveMember_ShouldRefuseToStrandTheHousehold_WhenItIsTheLastOwner()
    {
        // Arrange
        using var owner = await FreshOwnerAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        var me = (await owner.GetAsync("/api/v1/users/me", Token)).Json!.Value.GetProperty("userId").GetGuid();

        // Act
        var response = await owner.DeleteAsync($"/api/v1/households/{householdId}/members/{me}", Token);

        // Assert
        // The last owner cannot leave, even voluntarily: a household with no
        // owner can never be administered again.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("households.last_owner", response.ProblemCode);
    }

    [Fact]
    public async Task RemoveMember_ShouldLetAMemberLeaveOnTheirOwn()
    {
        // Arrange
        var (owner, member) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        var memberId = await AddMemberAsync(householdId, member);

        // Act
        var response = await member.DeleteAsync($"/api/v1/households/{householdId}/members/{memberId}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var members = await owner.GetAsync($"/api/v1/households/{householdId}/members", Token);
        Assert.Equal(1, members.Json!.Value.GetProperty("items").GetArrayLength());
        owner.Dispose();
        member.Dispose();
    }

    [Fact]
    public async Task ChangeRole_ShouldPromoteAMember_AndThenLetTheOriginalOwnerLeave()
    {
        // Arrange
        var (owner, member) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        var memberId = await AddMemberAsync(householdId, member);
        var ownerId = (await owner.GetAsync("/api/v1/users/me", Token)).Json!.Value.GetProperty("userId").GetGuid();

        // Act
        var promoted = await PatchAsync(
            owner,
            $"/api/v1/households/{householdId}/members/{memberId}",
            etag: null,
            new { role = "owner" });
        var left = await owner.DeleteAsync($"/api/v1/households/{householdId}/members/{ownerId}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        owner.Dispose();
        member.Dispose();
    }

    [Fact]
    public async Task ChangeRole_ShouldRejectAnUnknownRole_NamingTheField()
    {
        // Arrange
        var (owner, member) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        var memberId = await AddMemberAsync(householdId, member);

        // Act
        var response = await PatchAsync(
            owner,
            $"/api/v1/households/{householdId}/members/{memberId}",
            etag: null,
            new { role = "overlord" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("households.invalid_role", response.ProblemCode);
        owner.Dispose();
        member.Dispose();
    }

    [Fact]
    public async Task Delete_ShouldBeRefusedForAPlainMember()
    {
        // Arrange
        var (owner, member) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        await AddMemberAsync(householdId, member);

        // Act
        var response = await member.DeleteAsync($"/api/v1/households/{householdId}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        owner.Dispose();
        member.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ApiResponse> PatchAsync(ApiClient client, string path, string? etag, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(body)
        };

        if (etag is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));
        }

        return await client.SendAsync(request, Token);
    }

    private static async Task<Guid> FirstHouseholdIdAsync(ApiClient client) =>
        (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

    private async Task<Guid> AddMemberAsync(Guid householdId, ApiClient member)
    {
        var memberId = (await member.GetAsync("/api/v1/users/me", Token))
            .Json!.Value.GetProperty("userId").GetGuid();

        // Invitations are a separate bead; membership is arranged directly so
        // the authorisation rules can be tested now.
        await using var session = postgres.NewSession();
        var executor = new DbExecutor(session);

        await executor.ExecuteAsync(
            """
            insert into household_members (household_id, user_id, role, joined_at)
            values (@householdId, @memberId, 'member', now());
            """,
            new { householdId, memberId },
            Token);

        return memberId;
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

    private async Task<ApiClient> FreshOwnerAsync()
    {
        await postgres.ResetAsync(Token);

        return await SignedInAsync("ada@example.com");
    }

    private async Task<(ApiClient Owner, ApiClient Other)> TwoUsersAsync()
    {
        await postgres.ResetAsync(Token);

        var owner = await SignedInAsync("ada@example.com");

        var settings = postgres.Api.Services.GetRequiredService<RegistrationSettings>();
        settings.OpenRegistration = true;
        settings.RequireInvitation = false;

        var other = await SignedInAsync("grace@example.com");

        return (owner, other);
    }
}
