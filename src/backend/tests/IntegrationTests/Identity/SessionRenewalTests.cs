using IntegrationTests.Fixtures;

namespace IntegrationTests.Identity;

/// <summary>Sliding session renewal, proven through the pipeline: there is no refresh token, so renewal happens on use.</summary>
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
        // Zero hours makes every request due, so renewal is observable without winding a clock.
        using var factory = await RenewingFactoryAsync("0");
        using var client = await SignedInClientAsync(factory);

        // Act
        var response = await client.GetAsync("/api/v1/sessions", Token);

        // Assert
        var cookies = SetCookies(response);
        var session = Assert.Single(cookies, cookie => cookie.StartsWith($"{SessionCookie}=", StringComparison.Ordinal));
        var csrf = Assert.Single(cookies, cookie => cookie.StartsWith($"{CsrfCookie}=", StringComparison.Ordinal));

        // Both together: a renewed session with a lapsed CSRF cookie could sign in but change nothing.
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

        // Otherwise the devices screen shows every session as last used when created.
        Assert.True(
            lastSeenAt > createdAt,
            $"Expected the session to have been touched, but last seen at {lastSeenAt} against created at {createdAt}.");
    }

    [Fact]
    public async Task AnAuthenticatedRequest_ShouldTouchNothing_WhenTheSessionWasJustUsed()
    {
        // Arrange
        // Default interval: a session seconds old is not due.
        using var client = await SignedInClientAsync(postgres.Api);

        // Act
        var response = await client.GetAsync("/api/v1/sessions", Token);

        // Assert
        // Renewing on every request would add a write to every page view.
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
        // Own-factory cases are reset by RenewingFactoryAsync; the shared host is reset here.
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
