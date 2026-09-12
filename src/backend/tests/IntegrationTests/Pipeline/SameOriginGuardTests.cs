using System.Text.Json;
using Api.Middleware;
using Application.Abstractions.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntegrationTests.Pipeline;

/// <summary>
/// The cheap check that runs before the CSRF token comparison, so a foreign
/// origin never reaches it.
/// </summary>
public class SameOriginGuardTests
{
    private const string OurOrigin = "https://culina.example";

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task SafeMethods_ShouldPassThrough_EvenFromAForeignOrigin(string method)
    {
        // Arrange
        var context = Request(method, origin: "https://evil.example", withSession: true);

        // Act
        var reached = await InvokeAsync(context);

        // Assert
        // Exempting safe methods is only sound because no GET endpoint in
        // Culina changes state.
        Assert.True(reached);
    }

    [Fact]
    public async Task UnsafeRequest_ShouldPassThrough_WhenItCarriesNoSessionCookie()
    {
        // Arrange
        var context = Request("POST", origin: null, withSession: false);

        // Act
        var reached = await InvokeAsync(context);

        // Assert
        // Without the cookie there is no ambient credential to abuse, so there
        // is nothing for a cross-site request to exploit.
        Assert.True(reached);
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeAccepted_WhenTheOriginIsOurs()
    {
        // Arrange
        var context = Request("POST", origin: OurOrigin, withSession: true);

        // Act
        var reached = await InvokeAsync(context);

        // Assert
        Assert.True(reached);
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeRejected_WhenTheOriginIsForeign()
    {
        // Arrange
        var context = Request("POST", origin: "https://evil.example", withSession: true);

        // Act
        var reached = await InvokeAsync(context);

        // Assert
        Assert.False(reached);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("auth.foreign_origin", await CodeAsync(context));
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeAccepted_WhenOnlyRefererIsPresentAndMatches()
    {
        // Arrange
        var context = Request("POST", origin: null, withSession: true);
        context.Request.Headers.Referer = $"{OurOrigin}/recipes/new";

        // Act
        var reached = await InvokeAsync(context);

        // Assert
        // Some browsers omit Origin on same-origin navigations, so Referer is
        // the documented fallback.
        Assert.True(reached);
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeRejected_WhenItStatesNoOriginAtAll()
    {
        // Arrange
        var context = Request("POST", origin: null, withSession: true);

        // Act
        var reached = await InvokeAsync(context);

        // Assert
        // An unsafe cookie-authenticated request that will not say where it
        // came from does not get the benefit of the doubt.
        Assert.False(reached);
        Assert.Equal("auth.foreign_origin", await CodeAsync(context));
    }

    [Fact]
    public async Task Request_ShouldBeRejected_WhenCookiesAreNotSecureAndTheOriginIsForeign()
    {
        // Arrange
        // Local development drops the `__Host-` prefix, because a browser
        // refuses that cookie over plain HTTP. This guard used to look for the
        // production name only, so in development it saw no session and let
        // every foreign origin through — a security check behaving differently
        // from the one that ships is the one thing it must never do.
        var context = Request("POST", "https://not-culina.example", withSession: true, secureCookies: false);

        // Act
        var reached = await InvokeAsync(context, secureCookies: false);

        // Assert
        Assert.False(reached);
        Assert.Equal("auth.foreign_origin", await CodeAsync(context));
    }

    private static DefaultHttpContext Request(
        string method,
        string? origin,
        bool withSession,
        bool secureCookies = true)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("culina.example");
        context.Request.Path = "/api/v1/recipes";

        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        if (withSession)
        {
            var name = secureCookies ? CookieSettings.SessionCookieName : "culina.session";

            context.Request.Headers.Cookie = $"{name}=opaque";
        }

        return context;
    }

    private static async Task<bool> InvokeAsync(HttpContext context, bool secureCookies = true)
    {
        var reached = false;

        var middleware = new SameOriginMiddleware(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            new CookieSettings { Secure = secureCookies },
            NullLogger<SameOriginMiddleware>.Instance);

        return reached;
    }

    private static async Task<string?> CodeAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;

        var document = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);

        return document.GetProperty("code").GetString();
    }
}
