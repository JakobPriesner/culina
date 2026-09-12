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
        // Arrange
        await using var scope = await NewScopeAsync();
        var owner = await scope.AddUserAsync("owner@example.com");
        var household = AHousehold(owner);

        // Act
        await scope.Households.AddAsync(household, Token);
        var found = await scope.Households.FindAsync(household.Id, Token);

        // Assert
        var stored = found.ShouldBeSuccess();
        Assert.Equal("Kitchen", stored.Name.Value);
        var member = Assert.Single(stored.Members);
        Assert.Equal(owner, member.UserId);
        Assert.Equal(HouseholdRole.Owner, member.Role);
    }

    [Fact]
    public async Task Update_ShouldPersistBothTheNameAndTheMembership_InOneTransaction()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var owner = await scope.AddUserAsync("owner@example.com");
        var joiner = await scope.AddUserAsync("joiner@example.com");
        var household = AHousehold(owner);
        await scope.Households.AddAsync(household, Token);

        household.Rename(HouseholdName.Create("The Kitchen").ShouldBeSuccess(), owner).ShouldBeSuccess();
        household.Add(joiner, HouseholdRole.Member, Now).ShouldBeSuccess();

        // Act
        var version = await scope.Households.UpdateAsync(household, expectedVersion: 1, Token);

        // Assert
        Assert.Equal(2, version.ShouldBeSuccess());
        var reloaded = (await scope.Households.FindAsync(household.Id, Token)).ShouldBeSuccess();
        Assert.Equal("The Kitchen", reloaded.Name.Value);
        Assert.Equal(2, reloaded.Members.Count);
    }

    [Fact]
    public async Task Update_ShouldFailThePrecondition_WhenSomeoneElseWroteFirst()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var owner = await scope.AddUserAsync("owner@example.com");
        var household = AHousehold(owner);
        await scope.Households.AddAsync(household, Token);
        await scope.Households.UpdateAsync(household, expectedVersion: 1, Token);

        // Act
        var result = await scope.Households.UpdateAsync(household, expectedVersion: 1, Token);

        // Assert
        result.ShouldBeFailure(ConcurrencyErrors.VersionMismatch);
    }

    [Fact]
    public async Task ForUser_ShouldReturnEveryHouseholdWithItsOwnMembers()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var owner = await scope.AddUserAsync("owner@example.com");
        var other = await scope.AddUserAsync("other@example.com");

        var first = AHousehold(owner, "Home");
        var second = AHousehold(owner, "Cabin");
        second.Add(other, HouseholdRole.Member, Now).ShouldBeSuccess();
        await scope.Households.AddAsync(first, Token);
        await scope.Households.AddAsync(second, Token);

        // Act
        var households = await scope.Households.ForUserAsync(owner, Token);

        // Assert
        Assert.Equal(2, households.Count);
        // Members must not bleed between households in the batched read.
        Assert.Equal(2, households.Single(h => h.Name.Value == "Cabin").Members.Count);
        Assert.Single(households.Single(h => h.Name.Value == "Home").Members);
    }

    [Fact]
    public async Task Delete_ShouldRemoveTheHouseholdAndItsMembership()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var owner = await scope.AddUserAsync("owner@example.com");
        var household = AHousehold(owner);
        await scope.Households.AddAsync(household, Token);

        // Act
        await scope.Households.DeleteAsync(household.Id, Token);

        // Assert
        var found = await scope.Households.FindAsync(household.Id, Token);
        found.ShouldBeFailure(HouseholdErrors.NotFound(household.Id));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
