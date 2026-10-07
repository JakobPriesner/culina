using System.Text.Json;
using Api.Middleware;
using Application.Abstractions.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntegrationTests.Pipeline;

/// <summary>The cheap origin check that runs before the CSRF token comparison.</summary>
public class SameOriginGuardTests
{
    private const string OurOrigin = "https://culina.example";

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task SafeMethods_ShouldPassThrough_EvenFromAForeignOrigin(string method)
    {
        var context = Request(method, origin: "https://evil.example", withSession: true);

        var reached = await InvokeAsync(context);

        // Sound only because no GET endpoint in Culina changes state.
        Assert.True(reached);
    }

    [Fact]
    public async Task UnsafeRequest_ShouldPassThrough_WhenItCarriesNoSessionCookie()
    {
        var context = Request("POST", origin: null, withSession: false);

        var reached = await InvokeAsync(context);

        // Without the cookie there is no ambient credential to abuse.
        Assert.True(reached);
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeAccepted_WhenTheOriginIsOurs()
    {
        var context = Request("POST", origin: OurOrigin, withSession: true);

        var reached = await InvokeAsync(context);

        Assert.True(reached);
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeRejected_WhenTheOriginIsForeign()
    {
        var context = Request("POST", origin: "https://evil.example", withSession: true);

        var reached = await InvokeAsync(context);

        Assert.False(reached);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("auth.foreign_origin", await CodeAsync(context));
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeAccepted_WhenOnlyRefererIsPresentAndMatches()
    {
        var context = Request("POST", origin: null, withSession: true);
        context.Request.Headers.Referer = $"{OurOrigin}/recipes/new";

        var reached = await InvokeAsync(context);

        // Some browsers omit Origin on same-origin navigations, so Referer is the fallback.
        Assert.True(reached);
    }

    [Fact]
    public async Task UnsafeRequest_ShouldBeRejected_WhenItStatesNoOriginAtAll()
    {
        var context = Request("POST", origin: null, withSession: true);

        var reached = await InvokeAsync(context);

        // An unsafe cookie-authenticated request that will not say where it came from gets no benefit of the doubt.
        Assert.False(reached);
        Assert.Equal("auth.foreign_origin", await CodeAsync(context));
    }

    [Fact]
    public async Task Request_ShouldBeRejected_WhenCookiesAreNotSecureAndTheOriginIsForeign()
    {
        // Development drops the `__Host-` prefix; the guard once missed that cookie name and let foreign origins through.
        var context = Request("POST", "https://not-culina.example", withSession: true, secureCookies: false);

        var reached = await InvokeAsync(context, secureCookies: false);

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
