using System.Text.Json;
using Api.Infrastructure;
using Api.Middleware;
using Domain.Sessions;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntegrationTests.Pipeline;

/// <summary>
/// The synchronizer token is the real CSRF defence; SameSite and the origin check are cheap layers
/// before it.
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
        var world = new CsrfWorld();
        var context = world.Request(method, csrfHeader: null);

        var reached = await world.InvokeAsync(context);

        // Exempting safe methods is sound only because no GET endpoint changes state.
        Assert.True(reached);
    }

    [Fact]
    public async Task UnsafeRequest_ShouldPassThrough_WhenThereIsNoSession()
    {
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: null, signedIn: false);

        var reached = await world.InvokeAsync(context);

        Assert.True(reached);
    }

    [Fact]
    public async Task ExemptEndpoint_ShouldPassThrough_WhenTheTokenIsLost()
    {
        // The wedge this prevents: CSRF cookie gone but session cookie alive, so every unsafe
        // request (even sign-out) fails.
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: null, exempt: true);

        var reached = await world.InvokeAsync(context);

        // Signing in again is the way out, so it cannot be blocked.
        Assert.True(reached);
    }

    [Fact]
    public async Task ExemptEndpoint_ShouldStillBeTheOnlyOneExempted()
    {
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: null, exempt: false);

        var reached = await world.InvokeAsync(context);

        Assert.False(reached);
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeRejected_WhenTheHeaderIsMissing()
    {
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: null);

        var reached = await world.InvokeAsync(context);

        Assert.False(reached);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("auth.csrf_invalid", await CodeAsync(context));
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeRejected_WhenTheTokenBelongsToAnotherSession()
    {
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: world.Tokens.NewToken());

        var reached = await world.InvokeAsync(context);

        // A guessable or reusable token would defeat the mechanism, so it is compared with the
        // stored digest.
        Assert.False(reached);
        Assert.Equal("auth.csrf_invalid", await CodeAsync(context));
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeRejected_WhenAuthenticationLeftNoDigest()
    {
        // A principal the session handler did not admit has no digest and is refused.
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: world.CsrfToken, withDigest: false);

        var reached = await world.InvokeAsync(context);

        Assert.False(reached);
        Assert.Equal("auth.csrf_invalid", await CodeAsync(context));
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeAccepted_WhenTheTokenMatchesTheSession()
    {
        var world = new CsrfWorld();
        var context = world.Request("POST", csrfHeader: world.CsrfToken);

        var reached = await world.InvokeAsync(context);

        Assert.True(reached);
    }

    private static async Task<string?> CodeAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;

        var document = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);

        return document.GetProperty("code").GetString();
    }

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
