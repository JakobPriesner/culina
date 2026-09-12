using Application.Abstractions;
using Domain.Shared;
using Domain.Users;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Users;
using IntegrationTests.Fixtures;
using TestSupport;

namespace IntegrationTests.Persistence;

[Collection(RequiresDatabase.Name)]
public class UserRepositoryTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAndFind_ShouldRoundTripTheUser_WhenStored()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var user = AUser("ada@example.com");

        // Act
        await scope.Users.AddAsync(user, isAdmin: false, Token);
        var found = await scope.Users.FindAsync(user.Id, Token);

        // Assert
        var stored = found.ShouldBeSuccess();
        Assert.Equal("ada@example.com", stored.Email.Value);
        Assert.Equal("Ada", stored.DisplayName.Value);
        Assert.Equal(1, stored.Version);
    }

    [Fact]
    public async Task FindByEmail_ShouldIgnoreCase_BecauseTheColumnIsCitext()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var user = AUser("grace@example.com");
        await scope.Users.AddAsync(user, isAdmin: false, Token);

        // Act
        var found = await scope.Users.FindByEmailAsync(
            Email.Create("GRACE@EXAMPLE.COM").ShouldBeSuccess(),
            Token);

        // Assert
        Assert.Equal(user.Id, found.ShouldBeSuccess().Id);
    }

    [Fact]
    public async Task Add_ShouldReportAConflict_WhenTheAddressIsAlreadyRegistered()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        await scope.Users.AddAsync(AUser("taken@example.com"), isAdmin: false, Token);

        // Act
        var result = await scope.Users.AddAsync(AUser("taken@example.com"), isAdmin: false, Token);

        // Assert
        // The database settles the race between two registrations, which is why
        // the unique violation is translated here rather than guessed at above.
        result.ShouldBeFailure(UserErrors.EmailAlreadyUsed);
    }

    [Fact]
    public async Task Update_ShouldBumpTheVersion_WhenTheExpectedVersionMatches()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var user = AUser("ada@example.com");
        await scope.Users.AddAsync(user, isAdmin: false, Token);
        user.ChangeDisplayName(DisplayName.Create("Ada Lovelace").ShouldBeSuccess());

        // Act
        var result = await scope.Users.UpdateAsync(user, expectedVersion: 1, Token);

        // Assert
        Assert.Equal(2, result.ShouldBeSuccess());
    }

    [Fact]
    public async Task Update_ShouldFailThePrecondition_WhenSomeoneElseWroteFirst()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var user = AUser("ada@example.com");
        await scope.Users.AddAsync(user, isAdmin: false, Token);
        await scope.Users.UpdateAsync(user, expectedVersion: 1, Token);

        // Act
        var result = await scope.Users.UpdateAsync(user, expectedVersion: 1, Token);

        // Assert
        // The check lives in the SQL WHERE; zero rows affected is the signal.
        result.ShouldBeFailure(ConcurrencyErrors.VersionMismatch);
    }

    [Fact]
    public async Task IsAdmin_ShouldBeTrue_OnlyForTheAccountThatAdministersTheInstance()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var admin = AUser("admin@example.com");
        var ordinary = AUser("member@example.com");
        await scope.Users.AddAsync(admin, isAdmin: true, Token);
        await scope.Users.AddAsync(ordinary, isAdmin: false, Token);

        // Act
        var adminFlag = await scope.Users.IsAdminAsync(admin.Id, Token);
        var ordinaryFlag = await scope.Users.IsAdminAsync(ordinary.Id, Token);

        // Assert
        Assert.True(adminFlag);
        Assert.False(ordinaryFlag);
    }

    [Fact]
    public async Task Find_ShouldReportNotFound_WhenNoSuchUserExists()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var missing = Guid.CreateVersion7();

        // Act
        var result = await scope.Users.FindAsync(missing, Token);

        // Assert
        result.ShouldBeFailure(UserErrors.NotFound(missing));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static User AUser(string email) => User.Register(
        Email.Create(email).ShouldBeSuccess(),
        DisplayName.Create("Ada").ShouldBeSuccess(),
        "argon2id$hash",
        Now);

    private async Task<RepositoryScope> NewScopeAsync()
    {
        _ = postgres.Api.Services;
        await postgres.ResetAsync(Token);

        var session = postgres.NewSession();

        return new RepositoryScope(session, new UserRepository(new DbExecutor(session)));
    }

    private sealed record RepositoryScope(DbSession Session, IUserRepository Users) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}
