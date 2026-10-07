using System.Text.Json;
using Api.Infrastructure;
using Api.Middleware;
using Domain.Sessions;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntegrationTests.Pipeline;

/// <summary>
/// The synchronizer token is the real CSRF defence; SameSite and the origin
/// check are the cheap layers in front of it.
/// </summary>
public class CsrfGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task SafeMethods_ShouldPassThrough_WithoutTheToken(string method)
    {
        // Arrange
        var world = new CsrfWorld();
        var context = world.Request(method, csrfHeader: null);

        // Act
        var reached = await world.InvokeAsync(context);

        // Assert
        // Exempting safe methods is sound only because no GET endpoint in
        // Culina changes state.
        Assert.True(reached);
    }

    [Fact]
    public async Task UnsafeRequest_ShouldPassThrough_WhenThereIsNoSession()
    {
        // Arrange
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: null, signedIn: false);

        // Act
        var reached = await world.InvokeAsync(context);

        // Assert
        // Without a session cookie there is no ambient credential to abuse.
        Assert.True(reached);
    }

    [Fact]
    public async Task ExemptEndpoint_ShouldPassThrough_WhenTheTokenIsLost()
    {
        // Arrange
        // The wedge this exists to prevent: the readable CSRF cookie is gone
        // but the HttpOnly session cookie survives, so every unsafe request
        // fails — including the sign-out that would clear the session.
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: null, exempt: true);

        // Act
        var reached = await world.InvokeAsync(context);

        // Assert
        // Signing in again is the way out, so signing in cannot be blocked.
        Assert.True(reached);
    }

    [Fact]
    public async Task ExemptEndpoint_ShouldStillBeTheOnlyOneExempted()
    {
        // Arrange
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: null, exempt: false);

        // Act
        var reached = await world.InvokeAsync(context);

        // Assert
        // The exemption is per endpoint, not a hole in the guard.
        Assert.False(reached);
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeRejected_WhenTheHeaderIsMissing()
    {
        // Arrange
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: null);

        // Act
        var reached = await world.InvokeAsync(context);

        // Assert
        Assert.False(reached);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("auth.csrf_invalid", await CodeAsync(context));
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeRejected_WhenTheTokenBelongsToAnotherSession()
    {
        // Arrange
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: world.Tokens.NewToken());

        // Act
        var reached = await world.InvokeAsync(context);

        // Assert
        // A token the attacker can guess or reuse would defeat the whole
        // mechanism, so it is compared against this session's stored digest.
        Assert.False(reached);
        Assert.Equal("auth.csrf_invalid", await CodeAsync(context));
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeRejected_WhenAuthenticationLeftNoDigest()
    {
        // Arrange
        // A principal the session handler did not admit has no digest to
        // compare against, and is refused rather than waved through.
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: world.CsrfToken, withDigest: false);

        // Act
        var reached = await world.InvokeAsync(context);

        // Assert
        Assert.False(reached);
        Assert.Equal("auth.csrf_invalid", await CodeAsync(context));
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeAccepted_WhenTheTokenMatchesTheSession()
    {
        // Arrange
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: world.CsrfToken);

        // Act
        var reached = await world.InvokeAsync(context);

        // Assert
        Assert.True(reached);
    }

    private static async Task<string?> CodeAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;

        var document = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);

        return document.GetProperty("code").GetString();
    }

    /// <summary>A session that exists, and the CSRF token that belongs to it.</summary>
    private sealed class CsrfWorld
    {
        internal SecretTokens Tokens { get; } = new();

        internal string CsrfToken { get; }

        private readonly Session session;

        internal CsrfWorld()
        {
            CsrfToken = Tokens.NewToken();

            session = Session.Start(
                Guid.CreateVersion7(),
                Tokens.Digest(Tokens.NewToken()),
                Tokens.Digest(CsrfToken),
                Now,
                TimeSpan.FromDays(30),
                ipAddress: null,
                userAgent: null);
        }

        internal DefaultHttpContext Request(
            string method,
            string? csrfHeader,
            bool signedIn = true,
            bool exempt = false,
            bool withDigest = true)
        {
            var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

            context.Request.Method = method;
            context.Request.Path = "/api/v1/recipes";

            if (exempt)
            {
                context.SetEndpoint(
                    new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new CsrfExempt()), "exempt"));
            }

            if (signedIn)
            {
                context.User = PrincipalFor(session);
            }

            // What the session handler leaves behind when it admits a session.
            if (signedIn && withDigest)
            {
                RequestContext.SetCsrfTokenHash(context, session.CsrfTokenHash);
            }

            if (csrfHeader is not null)
            {
                context.Request.Headers["X-Culina-CSRF"] = csrfHeader;
            }

            return context;
        }

        internal async Task<bool> InvokeAsync(HttpContext context)
        {
            var reached = false;

            var middleware = new CsrfMiddleware(_ =>
            {
                reached = true;
                return Task.CompletedTask;
            });

            await middleware.InvokeAsync(context, Tokens, NullLogger<CsrfMiddleware>.Instance);

            return reached;
        }

        private static System.Security.Claims.ClaimsPrincipal PrincipalFor(Session session) =>
            new(new System.Security.Claims.ClaimsIdentity(
                [
                    new System.Security.Claims.Claim("sub", session.UserId.ToString()),
                    new System.Security.Claims.Claim("sid", session.Id.ToString())
                ],
                "culina.session"));
    }
}
