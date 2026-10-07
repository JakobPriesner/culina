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
        using var owner = await FreshOwnerAsync();

        var response = await owner.PostAsync("/api/v1/households", new { name = "Cabin" }, Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("owner", response.Json!.Value.GetProperty("yourRole").GetString());
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task GetAll_ShouldListTheFirstHouseholdAndAnyOthers()
    {
        using var owner = await FreshOwnerAsync();
        await owner.PostAsync("/api/v1/households", new { name = "Cabin" }, Token);

        var response = await owner.GetAsync("/api/v1/households", Token);

        // One created with the first account, one created just now.
        Assert.Equal(2, response.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetById_ShouldReturn404ForANonMember_NotAForbidden()
    {
        var (owner, stranger) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);

        var response = await stranger.GetAsync($"/api/v1/households/{householdId}", Token);

        // "Forbidden" would confirm the household exists, which a non-member has no business
        // learning.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("households.not_found", response.ProblemCode);
        owner.Dispose();
        stranger.Dispose();
    }

    [Fact]
    public async Task GetById_ShouldAnswer304_WhenNothingHasChanged()
    {
        using var owner = await FreshOwnerAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        var first = await owner.GetAsync($"/api/v1/households/{householdId}", Token);

        var again = await GetIfNoneMatchAsync(owner, householdId, first.ETag!);

        Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
        Assert.Equal(first.ETag, again.ETag);
    }

    [Fact]
    public async Task GetById_ShouldTagEachRoleApart_BecauseTheBodySaysWhichOneYouHold()
    {
        // Same household, same version, different body: on a shared device a shared tag would
        // answer 304 over somebody else's role.
        var (owner, member) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        await AddMemberAsync(householdId, member);

        var ownersCopy = await owner.GetAsync($"/api/v1/households/{householdId}", Token);

        var membersRead = await GetIfNoneMatchAsync(member, householdId, ownersCopy.ETag!);

        Assert.Equal(HttpStatusCode.OK, membersRead.StatusCode);
        Assert.Equal("member", membersRead.Json!.Value.GetProperty("yourRole").GetString());
        Assert.NotEqual(ownersCopy.ETag, membersRead.ETag);
        owner.Dispose();
        member.Dispose();
    }

    [Fact]
    public async Task GetById_ShouldChangeItsTag_WhenAMemberRenamesThemselves()
    {
        // A new name writes the account, not the household, so the household's version stays.
        var (owner, member) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        await AddMemberAsync(householdId, member);

        var before = await owner.GetAsync($"/api/v1/households/{householdId}", Token);
        var account = await member.GetAsync("/api/v1/users/me", Token);
        await PatchAsync(member, "/api/v1/users/me", account.ETag!, new { displayName = "Grace Hopper" });

        var after = await GetIfNoneMatchAsync(owner, householdId, before.ETag!);

        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Contains(
            after.Json!.Value.GetProperty("members").EnumerateArray(),
            m => m.GetProperty("displayName").GetString() == "Grace Hopper");
        owner.Dispose();
        member.Dispose();
    }

    [Fact]
    public async Task Delete_ShouldRequireIfMatch_AndRejectAStaleOne()
    {
        using var owner = await FreshOwnerAsync();
        var householdId = await CabinAsync(owner);
        var stale = (await owner.GetAsync($"/api/v1/households/{householdId}", Token)).ETag!;

        await PatchAsync(owner, $"/api/v1/households/{householdId}", stale, new { name = "Hut" });

        var missing = await owner.DeleteAsync($"/api/v1/households/{householdId}", Token);
        var outdated = await owner.DeleteAsync($"/api/v1/households/{householdId}", stale, Token);

        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, outdated.StatusCode);

        var stillThere = await owner.GetAsync($"/api/v1/households/{householdId}", Token);

        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldSucceedForAnOwner_WithACurrentIfMatch()
    {
        using var owner = await FreshOwnerAsync();
        var householdId = await CabinAsync(owner);

        var response = await owner.DeleteCurrentAsync($"/api/v1/households/{householdId}", Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var gone = await owner.GetAsync($"/api/v1/households/{householdId}", Token);

        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Rename_ShouldBeRefusedForAPlainMember_EvenThoughTheyCanSeeIt()
    {
        var (owner, member) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        await AddMemberAsync(householdId, member);

        var current = await member.GetAsync($"/api/v1/households/{householdId}", Token);

        var response = await PatchAsync(member, $"/api/v1/households/{householdId}", current.ETag!, new { name = "Mine now" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("households.not_owner", response.ProblemCode);
        owner.Dispose();
        member.Dispose();
    }

    [Fact]
    public async Task Rename_ShouldSucceedForAnOwner_WithACurrentIfMatch()
    {
        using var owner = await FreshOwnerAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        var current = await owner.GetAsync($"/api/v1/households/{householdId}", Token);

        var response = await PatchAsync(owner, $"/api/v1/households/{householdId}", current.ETag!, new { name = "The Kitchen" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("The Kitchen", response.Json!.Value.GetProperty("name").GetString());
    }

    [Fact]
    public async Task RemoveMember_ShouldRefuseToStrandTheHousehold_WhenItIsTheLastOwner()
    {
        using var owner = await FreshOwnerAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        var me = (await owner.GetAsync("/api/v1/users/me", Token)).Json!.Value.GetProperty("userId").GetGuid();

        var response = await owner.DeleteAsync($"/api/v1/households/{householdId}/members/{me}", Token);

        // The last owner cannot leave, even voluntarily: a household with no
        // owner can never be administered again.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("households.last_owner", response.ProblemCode);
    }

    [Fact]
    public async Task RemoveMember_ShouldLetAMemberLeaveOnTheirOwn()
    {
        var (owner, member) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        var memberId = await AddMemberAsync(householdId, member);

        var response = await member.DeleteAsync($"/api/v1/households/{householdId}/members/{memberId}", Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var members = await owner.GetAsync($"/api/v1/households/{householdId}/members", Token);
        Assert.Equal(1, members.Json!.Value.GetProperty("items").GetArrayLength());
        owner.Dispose();
        member.Dispose();
    }

    [Fact]
    public async Task ChangeRole_ShouldPromoteAMember_AndThenLetTheOriginalOwnerLeave()
    {
        var (owner, member) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        var memberId = await AddMemberAsync(householdId, member);
        var ownerId = (await owner.GetAsync("/api/v1/users/me", Token)).Json!.Value.GetProperty("userId").GetGuid();

        var promoted = await PatchAsync(
            owner,
            $"/api/v1/households/{householdId}/members/{memberId}",
            etag: null,
            new { role = "owner" });
        var left = await owner.DeleteAsync($"/api/v1/households/{householdId}/members/{ownerId}", Token);

        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        owner.Dispose();
        member.Dispose();
    }

    [Fact]
    public async Task ChangeRole_ShouldRejectAnUnknownRole_NamingTheField()
    {
        var (owner, member) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        var memberId = await AddMemberAsync(householdId, member);

        var response = await PatchAsync(
            owner,
            $"/api/v1/households/{householdId}/members/{memberId}",
            etag: null,
            new { role = "overlord" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("households.invalid_role", response.ProblemCode);
        owner.Dispose();
        member.Dispose();
    }

    [Fact]
    public async Task Delete_ShouldBeRefusedForAPlainMember()
    {
        var (owner, member) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        await AddMemberAsync(householdId, member);

        var response = await member.DeleteCurrentAsync($"/api/v1/households/{householdId}", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("households.not_owner", response.ProblemCode);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/v1/households/{householdId}", Token)).StatusCode);
        owner.Dispose();
        member.Dispose();
    }

    [Fact]
    public async Task Delete_ShouldAnswerTheSame_ForAStrangerAndForNothingAtAll()
    {
        var (owner, stranger) = await TwoUsersAsync();
        var householdId = await FirstHouseholdIdAsync(owner);
        var etag = (await owner.GetAsync($"/api/v1/households/{householdId}", Token)).ETag!;

        var theirs = await stranger.DeleteAsync($"/api/v1/households/{householdId}", etag, Token);
        var nothing = await stranger.DeleteAsync($"/api/v1/households/{Guid.NewGuid()}", etag, Token);

        // Telling the two apart would confirm which household ids exist, and a success must not
        // mean anything was deleted.
        Assert.Equal(nothing.StatusCode, theirs.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/households/{householdId}", Token)).StatusCode);
        owner.Dispose();
        stranger.Dispose();
    }

    [Fact]
    public async Task Delete_ShouldAnswer204Again_ForAHouseholdAlreadyInTheBin()
    {
        using var owner = await FreshOwnerAsync();
        var householdId = await CabinAsync(owner);
        var etag = (await owner.GetAsync($"/api/v1/households/{householdId}", Token)).ETag!;
        await owner.DeleteAsync($"/api/v1/households/{householdId}", etag, Token);

        // A second tap, with the version the first one was sent with.
        var again = await owner.DeleteAsync($"/api/v1/households/{householdId}", etag, Token);

        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
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

    private static async Task<ApiResponse> GetIfNoneMatchAsync(ApiClient client, Guid householdId, string etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/households/{householdId}");
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));

        return await client.SendAsync(request, Token);
    }

    /// <summary>A second household, so deleting it leaves the account one to live in.</summary>
    private static async Task<Guid> CabinAsync(ApiClient owner) =>
        (await owner.PostAsync("/api/v1/households", new { name = "Cabin" }, Token))
            .Json!.Value.GetProperty("householdId").GetGuid();

    private static async Task<Guid> FirstHouseholdIdAsync(ApiClient client) =>
        (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

    private async Task<Guid> AddMemberAsync(Guid householdId, ApiClient member)
    {
        var memberId = (await member.GetAsync("/api/v1/users/me", Token))
            .Json!.Value.GetProperty("userId").GetGuid();

        // Arranged directly: joining by invitation is not what is under test here.
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
