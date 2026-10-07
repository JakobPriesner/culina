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
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, token) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(session, Token);

        var found = await scope.Sessions.FindActiveByTokenAsync(token, Now, Token);

        Assert.Equal(session.Id, found.ShouldBeSuccess().Id);
    }

    [Fact]
    public async Task FindActiveByToken_ShouldFindNothing_WhenTheTokenIsWrong()
    {
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, _) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(session, Token);

        var found = await scope.Sessions.FindActiveByTokenAsync(scope.Tokens.NewToken(), Now, Token);

        found.ShouldBeFailure(SessionErrors.NotAuthenticated);
    }

    [Fact]
    public async Task Revoke_ShouldEndTheSession_WhenItBelongsToTheCaller()
    {
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, token) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(session, Token);

        var result = await scope.Sessions.RevokeAsync(session.Id, userId, Now, Token);

        result.ShouldBeSuccess();
        var found = await scope.Sessions.FindActiveByTokenAsync(token, Now, Token);
        found.ShouldBeFailure(SessionErrors.NotAuthenticated);
    }

    [Fact]
    public async Task FindActiveByToken_ShouldFindNothing_WhenTheSessionWasRevoked()
    {
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, token) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(session, Token);
        await scope.Sessions.RevokeAsync(session.Id, userId, Now, Token);

        var found = await scope.Sessions.FindActiveByTokenAsync(token, Now.AddMinutes(1), Token);

        // The CSRF guard also looks sessions up here without re-checking they are active, so a revoked token must not pass.
        found.ShouldBeFailure(SessionErrors.NotAuthenticated);
    }

    [Fact]
    public async Task FindActiveByToken_ShouldFindNothing_WhenTheSessionHasExpired()
    {
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, token) = scope.NewSession(userId, lifetime: TimeSpan.FromMinutes(1));
        await scope.Sessions.AddAsync(session, Token);

        var found = await scope.Sessions.FindActiveByTokenAsync(token, Now.AddMinutes(1), Token);

        found.ShouldBeFailure(SessionErrors.NotAuthenticated);
    }

    [Fact]
    public async Task Revoke_ShouldRefuse_WhenTheSessionBelongsToSomeoneElse()
    {
        await using var scope = await NewScopeAsync();
        var owner = await scope.AddUserAsync("ada@example.com");
        var stranger = await scope.AddUserAsync("mallory@example.com");
        var (session, _) = scope.NewSession(owner);
        await scope.Sessions.AddAsync(session, Token);

        var result = await scope.Sessions.RevokeAsync(session.Id, stranger, Now, Token);

        // Ownership is in the SQL WHERE so no second call site can forget it.
        result.ShouldBeFailure(SessionErrors.SessionNotFound);
    }

    [Fact]
    public async Task ForUser_ShouldListOnlyLiveSessions_SoTheDevicesScreenIsUseful()
    {
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (live, _) = scope.NewSession(userId);
        var (revoked, _) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(live, Token);
        await scope.Sessions.AddAsync(revoked, Token);
        await scope.Sessions.RevokeAsync(revoked.Id, userId, Now, Token);

        var sessions = await scope.Sessions.ForUserAsync(userId, Token);

        Assert.Equal(live.Id, Assert.Single(sessions).Id);
    }

    [Fact]
    public async Task Renew_ShouldMoveTheExpiry_WhenTheSessionIsStillActive()
    {
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, token) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(session, Token);
        session.Touch(Now.AddDays(20), Lifetime, TimeSpan.FromDays(90));

        var renewed = await scope.Sessions.RenewAsync(session, Token);

        renewed.ShouldBeSuccess();
        (await scope.Sessions.FindActiveByTokenAsync(token, Now.AddDays(40), Token)).ShouldBeSuccess();
    }

    [Fact]
    public async Task Renew_ShouldLeaveTheSessionRevoked_WhenItWasRevokedAfterBeingRead()
    {
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (session, token) = scope.NewSession(userId);
        await scope.Sessions.AddAsync(session, Token);

        // A stolen-cookie request reads the session, then the owner revokes that device before it renews.
        var read = (await scope.Sessions.FindActiveByTokenAsync(token, Now, Token)).ShouldBeSuccess();
        await scope.Sessions.RevokeAsync(session.Id, userId, Now.AddSeconds(1), Token);
        read.Touch(Now.AddSeconds(2), Lifetime, TimeSpan.FromDays(90));

        var renewed = await scope.Sessions.RenewAsync(read, Token);

        // Renewal once wrote the stale copy's revoked_at (null) back, reviving the revoked session.
        renewed.ShouldBeFailure(SessionErrors.NotAuthenticated);
        (await scope.Sessions.FindActiveByTokenAsync(token, Now.AddMinutes(1), Token))
            .ShouldBeFailure(SessionErrors.NotAuthenticated);
    }

    [Fact]
    public async Task DeleteExpired_ShouldRemoveLapsedSessions_ButKeepLiveOnes()
    {
        await using var scope = await NewScopeAsync();
        var userId = await scope.AddUserAsync("ada@example.com");
        var (live, liveToken) = scope.NewSession(userId);
        var (lapsed, _) = scope.NewSession(userId, lifetime: TimeSpan.FromMinutes(1));
        await scope.Sessions.AddAsync(live, Token);
        await scope.Sessions.AddAsync(lapsed, Token);

        var removed = await scope.Sessions.DeleteExpiredAsync(Now.AddHours(1), Token);

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
