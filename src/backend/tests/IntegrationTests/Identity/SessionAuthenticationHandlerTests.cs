using System.Text.Encodings.Web;
using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Sessions;
using Domain.Shared;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Identity;

/// <summary>
/// The moment between reading a session and renewing it, which no request
/// through the pipeline can be made to stop at.
/// </summary>
public class SessionAuthenticationHandlerTests
{
    private const string SessionToken = "the-cookie-value";
    private const string CsrfToken = "the-csrf-token";

    private static readonly DateTimeOffset Now = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Authenticate_ShouldLetTheRequestIn_WhenTheRenewalIsWritten()
    {
        // Arrange
        var (handler, _) = await HandlerAsync(renewal: Result.Success());

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Authenticate_ShouldLeaveTheSessionsCsrfDigest_WhenItLetsTheRequestIn()
    {
        // Arrange
        var (handler, context) = await HandlerAsync(renewal: Result.Success());

        // Act
        await handler.AuthenticateAsync();

        // Assert
        // CsrfMiddleware compares against this instead of reading the session
        // a second time.
        var digest = RequestContext.CsrfTokenHash(context);
        Assert.NotNull(digest);
        Assert.True(new SecretTokens().Matches(CsrfToken, digest.Value));
    }

    [Fact]
    public async Task Authenticate_ShouldRefuse_WhenTheSessionWasRevokedBeforeItsRenewal()
    {
        // Arrange
        // The store reports what it does when the revocation landed between
        // the read and the renewal: it refuses to touch the row.
        var (handler, context) = await HandlerAsync(renewal: SessionErrors.NotAuthenticated);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        Assert.Null(RequestContext.CsrfTokenHash(context));
    }

    private static async Task<(SessionAuthenticationHandler Handler, HttpContext Context)> HandlerAsync(Result renewal)
    {
        var tokens = new SecretTokens();
        var session = Session.Start(
            Guid.CreateVersion7(),
            tokens.Digest(SessionToken),
            tokens.Digest(CsrfToken),
            Now.AddDays(-2),
            TimeSpan.FromDays(30),
            "203.0.113.7",
            "Culina Tests");

        // Zero hours: every request is due for renewal.
        var cookies = new CookieSettings { Secure = false, RenewAfterHours = 0 };
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{SessionCookies.Name(cookies)}={SessionToken}";

        var handler = new SessionAuthenticationHandler(
            new SchemeOptions(),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            new RenewingStore(session, renewal),
            cookies,
            new FixedTime(Now));

        await handler.InitializeAsync(
            new AuthenticationScheme(CulinaClaims.Scheme, null, typeof(SessionAuthenticationHandler)),
            context);

        return (handler, context);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SchemeOptions : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue { get; } = new();

        public AuthenticationSchemeOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }

    /// <summary>Finds the one session, and answers its renewal as told.</summary>
    private sealed class RenewingStore(Session session, Result renewal) : ISessionStore
    {
        public Task<Result<Session>> FindActiveByTokenAsync(
            string token,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<Session>.Success(session));

        public Task<Result> RenewAsync(Session value, CancellationToken cancellationToken) =>
            Task.FromResult(renewal);

        public Task<IReadOnlyList<Session>> ForUserAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Session>>([session]);

        public Task<Result> AddAsync(Session value, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result> RevokeAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task RevokeAllAsync(Guid userId, Guid? keepSessionId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(0);
    }
}
