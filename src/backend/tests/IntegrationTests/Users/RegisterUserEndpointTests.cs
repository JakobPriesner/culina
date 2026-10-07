using System.Net;
using Application.Abstractions.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Users;

[Collection(RequiresDatabase.Name)]
public class RegisterUserEndpointTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Register_ShouldCreateTheAdministratorAndAHousehold_WhenTheInstanceIsEmpty()
    {
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();

        var response = await client.PostAsync("/api/v1/users", Body("ada@example.com"), Token);

        // A fresh instance needs a way in: the first account succeeds and becomes the administrator.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = response.Json!.Value;
        Assert.True(created.GetProperty("isAdmin").GetBoolean());
        Assert.NotEqual(Guid.Empty, created.GetProperty("householdId").GetGuid());
        Assert.Equal("ada@example.com", created.GetProperty("email").GetString());
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Register_ShouldNormaliseTheAddress_SoSignInIsUnambiguous()
    {
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();

        var response = await client.PostAsync("/api/v1/users", Body("  Ada@EXAMPLE.com "), Token);

        Assert.Equal("ada@example.com", response.Json!.Value.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Register_ShouldBeClosed_ForTheSecondAccount_WhenTheAdminHasNotOpenedIt()
    {
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();
        await client.PostAsync("/api/v1/users", Body("first@example.com"), Token);

        var response = await client.PostAsync("/api/v1/users", Body("second@example.com"), Token);

        // Closed by default, so a freshly online instance is not filled with accounts before its owner finishes setup.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("users.registration_closed", response.ProblemCode);
    }

    [Fact]
    public async Task Register_ShouldReportEveryBadFieldAtOnce_SoTheFormCanMarkThemAll()
    {
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();

        var response = await client.PostAsync(
            "/api/v1/users",
            new { email = "not-an-address", displayName = "  ", password = "short" },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = response.Json!.Value.GetProperty("errors").EnumerateArray()
            .Select(cause => cause.GetProperty("field").GetString())
            .ToList();
        Assert.Equal(["email", "displayName", "password"], fields);
    }

    [Fact]
    public async Task Register_ShouldRejectAShortPassword_AtTheBoundary()
    {
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();

        var response = await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = new string('a', 11) },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("users.weak_password", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_ShouldNeverEchoThePassword_InAnyForm()
    {
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();
        const string password = "correct horse battery staple";

        var response = await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password },
            Token);

        Assert.DoesNotContain(password, response.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("argon2", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_ShouldNotRequireACsrfToken_BecauseThereIsNoSessionYet()
    {
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();

        var response = await client.PostAsync("/api/v1/users", Body("ada@example.com"), Token);

        // The CSRF guard covers cookie-authenticated requests; sign-up has no ambient credential.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShouldMakeExactlyOneAdministrator_WhenFirstRegistrationsRace()
    {
        await postgres.ResetAsync(Token);

        var responses = await RegisterAllAtOnceAsync(8);

        // Each saw an empty instance on arrival; only one may act on that, the rest face the (closed) registration policy.
        var created = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.True(created.Json!.Value.GetProperty("isAdmin").GetBoolean());
        Assert.All(
            responses.Where(response => response != created),
            refused => Assert.Equal("users.registration_closed", refused.ProblemCode));

        Assert.Equal(1L, await postgres.QuerySingleAsync<long>("select count(*) from users where is_admin;", Token));
        Assert.Equal(1L, await postgres.QuerySingleAsync<long>("select count(*) from households;", Token));
    }

    [Fact]
    public async Task Register_ShouldStopAtTheUserLimit_WhenRegistrationsRaceForTheLastPlace()
    {
        await postgres.ResetAsync(Token);
        using var first = postgres.Api.NewApiClient();
        await first.PostAsync("/api/v1/users", Body("first@example.com"), Token);

        var settings = postgres.Api.Services.GetRequiredService<RegistrationSettings>();
        settings.OpenRegistration = true;
        settings.RequireInvitation = false;
        settings.MaxUsers = 2;

        var responses = await RegisterAllAtOnceAsync(8);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Equal(2L, await postgres.QuerySingleAsync<long>("select count(*) from users;", Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Sends one registration per client, all released together so they overlap in flight.</summary>
    private async Task<IReadOnlyList<ApiResponse>> RegisterAllAtOnceAsync(int count)
    {
        var clients = Enumerable.Range(0, count).Select(_ => postgres.Api.NewApiClient()).ToList();

        try
        {
            return await Task.WhenAll(clients.Select((client, index) =>
                client.PostAsync("/api/v1/users", Body($"racer{index}@example.com"), Token)));
        }
        finally
        {
            clients.ForEach(client => client.Dispose());
        }
    }

    private static object Body(string email) => new
    {
        email,
        displayName = "Ada",
        password = "correct horse battery staple"
    };
}
