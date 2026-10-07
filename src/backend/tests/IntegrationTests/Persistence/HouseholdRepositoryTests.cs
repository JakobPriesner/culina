using Application.Abstractions;
using Domain.Households;
using Domain.Shared;
using Domain.Users;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Households;
using Infrastructure.Persistence.Users;
using IntegrationTests.Fixtures;
using TestSupport;

namespace IntegrationTests.Persistence;

[Collection(RequiresDatabase.Name)]
public class HouseholdRepositoryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAndFind_ShouldRoundTripTheHouseholdWithItsMembers()
    {
        await using var scope = await NewScopeAsync();
        var owner = await scope.AddUserAsync("owner@example.com");
        var household = AHousehold(owner);

        await scope.Households.AddAsync(household, Token);
        var found = await scope.Households.FindAsync(household.Id, Token);

        var stored = found.ShouldBeSuccess();
        Assert.Equal("Kitchen", stored.Name.Value);
        var member = Assert.Single(stored.Members);
        Assert.Equal(owner, member.UserId);
        Assert.Equal(HouseholdRole.Owner, member.Role);
    }

    [Fact]
    public async Task Update_ShouldPersistBothTheNameAndTheMembership_InOneTransaction()
    {
        await using var scope = await NewScopeAsync();
        var owner = await scope.AddUserAsync("owner@example.com");
        var joiner = await scope.AddUserAsync("joiner@example.com");
        var household = AHousehold(owner);
        await scope.Households.AddAsync(household, Token);

        household.Rename(HouseholdName.Create("The Kitchen").ShouldBeSuccess(), owner).ShouldBeSuccess();
        household.Add(joiner, HouseholdRole.Member, Now).ShouldBeSuccess();

        var version = await scope.Households.UpdateAsync(household, expectedVersion: 1, Token);

        Assert.Equal(2, version.ShouldBeSuccess());
        var reloaded = (await scope.Households.FindAsync(household.Id, Token)).ShouldBeSuccess();
        Assert.Equal("The Kitchen", reloaded.Name.Value);
        Assert.Equal(2, reloaded.Members.Count);
    }

    [Fact]
    public async Task Update_ShouldFailThePrecondition_WhenSomeoneElseWroteFirst()
    {
        await using var scope = await NewScopeAsync();
        var owner = await scope.AddUserAsync("owner@example.com");
        var household = AHousehold(owner);
        await scope.Households.AddAsync(household, Token);
        await scope.Households.UpdateAsync(household, expectedVersion: 1, Token);

        var result = await scope.Households.UpdateAsync(household, expectedVersion: 1, Token);

        result.ShouldBeFailure(ConcurrencyErrors.VersionMismatch);
    }

    [Fact]
    public async Task ForUser_ShouldReturnEveryHouseholdWithItsOwnMembers()
    {
        await using var scope = await NewScopeAsync();
        var owner = await scope.AddUserAsync("owner@example.com");
        var other = await scope.AddUserAsync("other@example.com");

        var first = AHousehold(owner, "Home");
        var second = AHousehold(owner, "Cabin");
        second.Add(other, HouseholdRole.Member, Now).ShouldBeSuccess();
        await scope.Households.AddAsync(first, Token);
        await scope.Households.AddAsync(second, Token);

        var households = await scope.Households.ForUserAsync(owner, Token);

        Assert.Equal(2, households.Count);
        // Members must not bleed between households in the batched read.
        Assert.Equal(2, households.Single(h => h.Name.Value == "Cabin").Members.Count);
        Assert.Single(households.Single(h => h.Name.Value == "Home").Members);
    }

    [Fact]
    public async Task Delete_ShouldHideTheHousehold_ButKeepItsMembershipForARestore()
    {
        await using var scope = await NewScopeAsync();
        var owner = await scope.AddUserAsync("owner@example.com");
        var household = AHousehold(owner);
        await scope.Households.AddAsync(household, Token);

        var result = await scope.Households.DeleteAsync(household.Id, household.Version, owner, Now, Token);

        result.ShouldBeSuccess();
        var found = await scope.Households.FindAsync(household.Id, Token);
        found.ShouldBeFailure(HouseholdErrors.NotFound(household.Id));
        Assert.False(await scope.Households.IsMemberAsync(household.Id, owner, Token));
    }

    [Fact]
    public async Task Delete_ShouldKeepTheHousehold_WhenTheVersionIsStale()
    {
        await using var scope = await NewScopeAsync();
        var owner = await scope.AddUserAsync("owner@example.com");
        var household = AHousehold(owner);
        await scope.Households.AddAsync(household, Token);

        var result = await scope.Households.DeleteAsync(household.Id, household.Version + 1, owner, Now, Token);

        result.ShouldBeFailure(ConcurrencyErrors.VersionMismatch);
        (await scope.Households.FindAsync(household.Id, Token)).ShouldBeSuccess();
    }

    [Fact]
    public async Task Update_ShouldKeepAnHeirsInheritance_WhenWhoeverSetItOnlyChangesRole()
    {
        await using var scope = await NewScopeAsync();
        var (parent, heir, setter) = await InheritingAsync(scope);
        parent.ChangeRole(setter, HouseholdRole.Owner, parent.Members[0].UserId).ShouldBeSuccess();

        (await scope.Households.UpdateAsync(parent, parent.Version, Token)).ShouldBeSuccess();

        // Saving the parent's members must not delete and re-insert them: the heir's link hangs off the setter's row.
        var reloaded = (await scope.Households.FindAsync(heir.Id, Token)).ShouldBeSuccess();
        Assert.Equal(parent.Id, reloaded.InheritsFrom);
        Assert.Equal(setter, reloaded.InheritsSetBy);
    }

    [Fact]
    public async Task DeletingAnAccount_ShouldEndTheInheritanceItSetUp()
    {
        // No endpoint does this; an operator deleting the row must be closed by the database itself.
        await using var scope = await NewScopeAsync();
        var (_, heir, setter) = await InheritingAsync(scope);

        await new DbExecutor(scope.Session).ExecuteAsync(
            "delete from users where id = @setter;",
            new { setter },
            Token);

        var reloaded = (await scope.Households.FindAsync(heir.Id, Token)).ShouldBeSuccess();
        Assert.Null(reloaded.InheritsFrom);
        Assert.Null(reloaded.InheritsSetBy);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A parent kitchen, a member of it, and that member's own household inheriting the parent.</summary>
    private static async Task<(Household Parent, Household Heir, Guid Setter)> InheritingAsync(RepositoryScope scope)
    {
        var owner = await scope.AddUserAsync("owner@example.com");
        var setter = await scope.AddUserAsync("setter@example.com");
        var parent = AHousehold(owner, "Parents");
        parent.Add(setter, HouseholdRole.Member, Now).ShouldBeSuccess();
        await scope.Households.AddAsync(parent, Token);

        var heir = AHousehold(setter, "Flat");
        heir.Inherit(parent, [parent.Id], setter).ShouldBeSuccess();
        await scope.Households.AddAsync(heir, Token);

        return (parent, heir, setter);
    }

    private static Household AHousehold(Guid owner, string name = "Kitchen") =>
        Household.Create(HouseholdName.Create(name).ShouldBeSuccess(), owner, Now);

    private async Task<RepositoryScope> NewScopeAsync()
    {
        _ = postgres.Api.Services;
        await postgres.ResetAsync(Token);

        var session = postgres.NewSession();
        var executor = new DbExecutor(session);

        return new RepositoryScope(
            session,
            new HouseholdRepository(executor),
            new UserRepository(executor));
    }

    private sealed record RepositoryScope(
        DbSession Session,
        IHouseholdRepository Households,
        IUserRepository Users) : IAsyncDisposable
    {
        internal async Task<Guid> AddUserAsync(string email)
        {
            var user = User.Register(
                Email.Create(email).ShouldBeSuccess(),
                DisplayName.Create("Ada").ShouldBeSuccess(),
                "argon2id$hash",
                Now);

            (await Users.AddAsync(user, isAdmin: false, Token)).ShouldBeSuccess();

            return user.Id;
        }

        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}
