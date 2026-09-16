using IntegrationTests.Fixtures;

namespace IntegrationTests.Identity;

/// <summary>
/// The sliding session, proven through the pipeline rather than the store.
/// </summary>
/// <remarks>
/// Culina issues no refresh token: the cookie is an opaque reference, so there
/// is nothing to exchange and a revoked session dies on the next request. What
/// takes its place is renewal on use — and it has to happen in the one place
/// every authenticated request passes through, or a person who opens the app
/// every day is still signed out a month after signing in.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class SessionRenewalTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";
    private const string SessionCookie = "culina.session";
    private const string CsrfCookie = "culina.csrf";

    [Fact]
    public async Task AnAuthenticatedRequest_ShouldReissueBothCookies_OnceTheSessionIsDueForRenewal()
    {
        // Arrange
        // Zero hours: every request is due, which is what makes the behaviour
        // observable without a clock that can be wound forward.
        using var factory = await RenewingFactoryAsync("0");
        using var client = await SignedInClientAsync(factory);

        // Act
        var response = await client.GetAsync("/api/v1/sessions", Token);

        // Assert
        var cookies = SetCookies(response);
        var session = Assert.Single(cookies, cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal));
        var csrf = Assert.Single(cookies, cookie => cookie.StartsWith($"{CsrfCookie}=", StringComparison.Ordinal));

        // Both, together: a browser holding a renewed session cookie and a
        // lapsed CSRF cookie is signed in but unable to change anything.
        Assert.Contains("expires=", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", csrf, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnAuthenticatedRequest_ShouldRecordThatTheSessionWasUsed_WhenItRenews()
    {
        // Arrange
        using var factory = await RenewingFactoryAsync("0");
        using var client = await SignedInClientAsync(factory);

        // Act
        var response = await client.GetAsync("/api/v1/sessions", Token);

        // Assert
        var device = response.Json!.Value.GetProperty("items")[0];
        var createdAt = device.GetProperty("createdAt").GetDateTimeOffset();
        var lastSeenAt = device.GetProperty("lastSeenAt").GetDateTimeOffset();

        // Without this the devices screen reports every session as last used
        // at the moment it was created, however long it has been in use.
        Assert.True(
            lastSeenAt > createdAt,
            $"Expected the session to have been touched, but last seen at {lastSeenAt} against created at {createdAt}.");
    }

    [Fact]
    public async Task AnAuthenticatedRequest_ShouldTouchNothing_WhenTheSessionWasJustUsed()
    {
        // Arrange
        // The default interval: a session seconds old is not due for renewal.
        using var client = await SignedInClientAsync(postgres.Api);

        // Act
        var response = await client.GetAsync("/api/v1/sessions", Token);

        // Assert
        // One indexed read is what an authenticated request costs. Renewing on
        // every request would add a write to every page view for no gain.
        Assert.DoesNotContain(
            SetCookies(response),
            cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal));

        var device = response.Json!.Value.GetProperty("items")[0];
        Assert.Equal(
            device.GetProperty("createdAt").GetDateTimeOffset(),
            device.GetProperty("lastSeenAt").GetDateTimeOffset());
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IReadOnlyList<string> SetCookies(ApiResponse response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies) ? [.. cookies] : [];

    private async Task<CulinaApiFactory> RenewingFactoryAsync(string renewAfterHours)
    {
        await postgres.ResetAsync(Token);

        return new CulinaApiFactory(
            postgres,
            new Dictionary<string, string> { ["Cookies:RenewAfterHours"] = renewAfterHours });
    }

    private async Task<ApiClient> SignedInClientAsync(CulinaApiFactory factory)
    {
        // The shared host is reset by RenewingFactoryAsync for the cases that
        // need their own; this one resets it itself.
        if (ReferenceEquals(factory, postgres.Api))
        {
            await postgres.ResetAsync(Token);
        }

        var client = factory.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);

        return client;
    }
}
