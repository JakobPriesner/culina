using Application.Abstractions;
using Domain.Sessions;
using Domain.Users;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Users;
using IntegrationTests.Fixtures;
using TestSupport;

namespace IntegrationTests.Identity;

[Collection(RequiresDatabase.Name)]
public class SessionStoreTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    [Fact]
    public async Task FindActiveByToken_ShouldReturnTheSession_WhenTheCookieValueIsCorrect()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, token) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(session, Token);

        // Act
        var found = await scope.Sessions.FindActiveByTokenAsync(token, Now, Token);

        // Assert
        Assert.Equal(session.Id, found.ShouldBeSuccess().Id);
    }

    [Fact]
    public async Task FindActiveByToken_ShouldFindNothing_WhenTheTokenIsWrong()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, _) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(session, Token);

        // Act
        var found = await scope.Sessions.FindActiveByTokenAsync(scope.Tokens.NewToken(), Now, Token);

        // Assert
        found.ShouldBeFailure(SessionErrors.NotAuthenticated);
    }

    [Fact]
    public async Task Revoke_ShouldEndTheSession_WhenItBelongsToTheCaller()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, token) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(session, Token);

        // Act
        var result = await scope.Sessions.RevokeAsync(session.Id, userId, Now, Token);

        // Assert
        result.ShouldBeSuccess();
        var found = await scope.Sessions.FindActiveByTokenAsync(token, Now, Token);
        found.ShouldBeFailure(SessionErrors.NotAuthenticated);
    }

    [Fact]
    public async Task FindActiveByToken_ShouldFindNothing_WhenTheSessionWasRevoked()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, token) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(session, Token);
        await scope.Sessions.RevokeAsync(session.Id, userId, Now, Token);

        // Act
        var found = await scope.Sessions.FindActiveByTokenAsync(token, Now.AddMinutes(1), Token);

        // Assert
        // The CSRF guard looks sessions up by this method too and does not
        // ask again whether they are active; a revoked session's token must
        // not pass it.
        found.ShouldBeFailure(SessionErrors.NotAuthenticated);
    }

    [Fact]
    public async Task FindActiveByToken_ShouldFindNothing_WhenTheSessionHasExpired()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, token) = scope.NewSession(userId, lifetime: TimeSpan.FromMinutes(1));
        await scope.Sessions.AddAsync(session, Token);

        // Act
        var found = await scope.Sessions.FindActiveByTokenAsync(token, Now.AddMinutes(1), Token);

        // Assert
        found.ShouldBeFailure(SessionErrors.NotAuthenticated);
    }

    [Fact]
    public async Task Revoke_ShouldRefuse_WhenTheSessionBelongsToSomeoneElse()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var owner = await scope.AddUserAsync("ada@example.com");
        var stranger = await scope.AddUserAsync("mallory@example.com");
        var (session, _) = scope.NewSession(owner);
        await scope.Sessions.AddAsync(session, Token);

        // Act
        var result = await scope.Sessions.RevokeAsync(session.Id, stranger, Now, Token);

        // Assert
        // Ownership is part of the SQL WHERE, so it cannot be forgotten at a
        // second call site.
        result.ShouldBeFailure(SessionErrors.SessionNotFound);
    }

    [Fact]
    public async Task ForUser_ShouldListOnlyLiveSessions_SoTheDevicesScreenIsUseful()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (live, _) = scope.NewSession(userId);
        var (revoked, _) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(live, Token);
        await scope.Sessions.AddAsync(revoked, Token);
        await scope.Sessions.RevokeAsync(revoked.Id, userId, Now, Token);

        // Act
        var sessions = await scope.Sessions.ForUserAsync(userId, Token);

        // Assert
        Assert.Equal(live.Id, Assert.Single(sessions).Id);
    }

    [Fact]
    public async Task Renew_ShouldMoveTheExpiry_WhenTheSessionIsStillActive()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, token) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(session, Token);
        session.Touch(Now.AddDays(20), Lifetime, TimeSpan.FromDays(90));

        // Act
        var renewed = await scope.Sessions.RenewAsync(session, Token);

        // Assert
        renewed.ShouldBeSuccess();
        (await scope.Sessions.FindActiveByTokenAsync(token, Now.AddDays(40), Token)).ShouldBeSuccess();
    }

    [Fact]
    public async Task Renew_ShouldLeaveTheSessionRevoked_WhenItWasRevokedAfterBeingRead()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, token) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(session, Token);

        // A request on a stolen cookie reads the session, then the owner
        // revokes that device before the request gets round to renewing it.
        var read = (await scope.Sessions.FindActiveByTokenAsync(token, Now, Token)).ShouldBeSuccess();
        await scope.Sessions.RevokeAsync(session.Id, userId, Now.AddSeconds(1), Token);
        read.Touch(Now.AddSeconds(2), Lifetime, TimeSpan.FromDays(90));

        // Act
        var renewed = await scope.Sessions.RenewAsync(read, Token);

        // Assert
        // Renewal used to write the stale copy's revoked_at (null) back, and
        // the revoked session worked again.
        renewed.ShouldBeFailure(SessionErrors.NotAuthenticated);
        (await scope.Sessions.FindActiveByTokenAsync(token, Now.AddMinutes(1), Token))
            .ShouldBeFailure(SessionErrors.NotAuthenticated);
    }

    [Fact]
    public async Task DeleteExpired_ShouldRemoveLapsedSessions_ButKeepLiveOnes()
    {
        // Arrange
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (live, liveToken) = scope.NewSession(userId);
        var (lapsed, _) = scope.NewSession(userId, lifetime: TimeSpan.FromMinutes(1));
        await scope.Sessions.AddAsync(live, Token);
        await scope.Sessions.AddAsync(lapsed, Token);

        // Act
        var removed = await scope.Sessions.DeleteExpiredAsync(Now.AddHours(1), Token);

        // Assert
        Assert.Equal(1, removed);
        (await scope.Sessions.FindActiveByTokenAsync(liveToken, Now, Token)).ShouldBeSuccess();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<SessionScope> NewScopeAsync()
    {
        _ = postgres.Api.Services;
        await postgres.ResetAsync(Token);

        var session = postgres.NewSession();
        var executor = new DbExecutor(session);
        var tokens = new SecretTokens();

        return new SessionScope(
            session,
            new SessionStore(executor, tokens),
            new UserRepository(executor),
            tokens);
    }

    private sealed record SessionScope(
        DbSession Db,
        ISessionStore Sessions,
        IUserRepository Users,
        ISecretTokens Tokens) : IAsyncDisposable
    {
        internal (Session Session, string Token) NewSession(Guid userId, TimeSpan? lifetime = null)
        {
            var token = Tokens.NewToken();
            var csrf = Tokens.NewToken();

            var session = Session.Start(
                userId,
                Tokens.Digest(token),
                Tokens.Digest(csrf),
                Now,
                lifetime ?? Lifetime,
                "203.0.113.7",
                "Culina Tests");

            return (session, token);
        }

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

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
