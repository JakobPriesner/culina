using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Endpoints;
using Api.Infrastructure;
using IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Pipeline;

[Collection(RequiresDatabase.Name)]
public class SecurityHeadersTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("Referrer-Policy", "no-referrer")]
    [InlineData("Cross-Origin-Opener-Policy", "same-origin")]
    [InlineData("Cross-Origin-Resource-Policy", "same-origin")]
    public async Task EveryResponse_ShouldCarryTheSecurityHeader_WhenHandled(
        string header,
        string expected)
    {
        // Arrange
        using var client = postgres.Api.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.Headers.TryGetValues(header, out var values), $"{header} was not set");
        Assert.Equal(expected, Assert.Single(values));
    }

    [Fact]
    public async Task ApiResponses_ShouldAllowNothingAtAll_WhenTheyAreJson()
    {
        // Arrange
        using var client = postgres.Api.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri("/api/v1/nothing-here", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        var policy = Policy(response);
        Assert.Contains("default-src 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocumentResponses_ShouldCarryAPerResponseNonce_WhenNotAnApiPath()
    {
        // Arrange
        using var client = postgres.Api.CreateClient();

        // Act
        using var first = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);
        using var second = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        var firstPolicy = Policy(first);
        var secondPolicy = Policy(second);
        Assert.Contains("script-src 'self' 'nonce-", firstPolicy, StringComparison.Ordinal);
        // A reused nonce is no better than unsafe-inline.
        Assert.NotEqual(firstPolicy, secondPolicy);
    }

    [Fact]
    public async Task DocumentResponses_ShouldNameTheNonceForStylesToo_SoTheBootScreenIsStyled()
    {
        // Arrange
        using var client = postgres.Api.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        // style-src 'self' on its own blocks the document's inline style block
        // and every style attribute in it, which showed up as a boot screen
        // rendering as unstyled text in production while looking correct under
        // the dev server, which sets no policy.
        Assert.Contains("style-src 'self' 'nonce-", Policy(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocumentResponses_ShouldRequireTrustedTypes_ForEveryScriptSink()
    {
        // Arrange
        using var client = postgres.Api.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        var policy = Policy(response);
        Assert.Contains("require-trusted-types-for 'script'", policy, StringComparison.Ordinal);
        Assert.Contains(
            "trusted-types svelte-trusted-html sveltekit-trusted-url culina-worker-url",
            policy,
            StringComparison.Ordinal);
        // 'allow-duplicates' and a catch-all would each let any code mint its own policy.
        Assert.DoesNotContain("allow-duplicates", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("trusted-types *", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DocumentResponses_ShouldAllowOneStyleAttribute_TheRouteAnnouncers()
    {
        // Arrange
        using var client = postgres.Api.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        // One hash and nothing else: a second source here would be a style
        // attribute somebody added without deciding to.
        var directive = Policy(response)
            .Split("; ")
            .Single(part => part.StartsWith("style-src-attr ", StringComparison.Ordinal));
        Assert.Equal(
            "style-src-attr 'unsafe-hashes' 'sha256-S8qMpvofolR8Mpjy4kQvEm7m1q8clzU4dfDH0AmvZjo='",
            directive);
    }

    [Fact]
    public async Task NoPolicy_ShouldEverAllowInlineOrEval_OnAnyPath()
    {
        // Arrange
        using var client = postgres.Api.CreateClient();
        string[] paths = ["/health/live", "/api/v1/nothing-here"];

        // Act & Assert
        foreach (var path in paths)
        {
            using var response = await client.GetAsync(
                new Uri(path, UriKind.Relative),
                TestContext.Current.CancellationToken);

            var policy = Policy(response);
            // Either of these disables the protection the rest of the policy
            // provides, so their absence is asserted rather than assumed.
            Assert.DoesNotContain("unsafe-inline", policy, StringComparison.Ordinal);
            Assert.DoesNotContain("unsafe-eval", policy, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task NoResponse_ShouldCarryHsts_BecauseTheProxyOwnsIt()
    {
        // Arrange
        using var client = postgres.Api.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task UnhandledException_ShouldStillCarryEverySecurityHeaderAndTheRequestId()
    {
        // Arrange
        using var api = new CulinaApiFactory(postgres);
        using var defective = api.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IEndpoint, ThrowingEndpoint>()));
        using var client = defective.CreateClient();

        // Act
        using var response = await client.GetAsync(
            new Uri(ThrowingEndpoint.Path, UriKind.Relative),
            TestContext.Current.CancellationToken);

        // Assert
        // The exception handler clears every header before it writes the
        // problem document, which is how a 500 used to go out with none.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Single(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Single(response, "Referrer-Policy"));
        Assert.Equal("same-origin", Single(response, "Cross-Origin-Opener-Policy"));
        Assert.Equal("same-origin", Single(response, "Cross-Origin-Resource-Policy"));
        Assert.Equal("camera=(self), microphone=(), geolocation=()", Single(response, "Permissions-Policy"));
        Assert.Contains("default-src 'none'", Policy(response), StringComparison.Ordinal);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        Assert.Equal("server.unexpected", problem.GetProperty("code").GetString());
        Assert.Equal(problem.GetProperty("requestId").GetString(), Single(response, "X-Request-Id"));
    }

    private static string Policy(HttpResponseMessage response) =>
        string.Join(" ", response.Headers.GetValues("Content-Security-Policy"));

    private static string Single(HttpResponseMessage response, string header)
    {
        Assert.True(response.Headers.TryGetValues(header, out var values), $"{header} was not set");

        return Assert.Single(values);
    }

    /// <summary>A defect, mapped only in the host this class builds for it.</summary>
    private sealed class ThrowingEndpoint : IEndpoint
    {
        internal const string Path = $"{ApiPaths.V1}/test-only/defect";

        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet(Path, IResult () => throw new InvalidOperationException("A defect, on purpose."))
                .AllowAnonymous();
    }
}
