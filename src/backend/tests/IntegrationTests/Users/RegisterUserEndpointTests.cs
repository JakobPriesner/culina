using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Users;

[Collection(RequiresDatabase.Name)]
public class RegisterUserEndpointTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Register_ShouldCreateTheAdministratorAndAHousehold_WhenTheInstanceIsEmpty()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();

        // Act
        var response = await client.PostAsync("/api/v1/users", Body("ada@example.com"), Token);

        // Assert
        // A fresh instance has to have a way in, so the first account always
        // succeeds and becomes the administrator.
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
        // Arrange
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();

        // Act
        var response = await client.PostAsync("/api/v1/users", Body("  Ada@EXAMPLE.com "), Token);

        // Assert
        Assert.Equal("ada@example.com", response.Json!.Value.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Register_ShouldBeClosed_ForTheSecondAccount_WhenTheAdminHasNotOpenedIt()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();
        await client.PostAsync("/api/v1/users", Body("first@example.com"), Token);

        // Act
        var response = await client.PostAsync("/api/v1/users", Body("second@example.com"), Token);

        // Assert
        // Registration is closed by default: an instance that opened it the
        // moment it came online would be filled with accounts before its owner
        // finished setting it up.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("users.registration_closed", response.ProblemCode);
    }

    [Fact]
    public async Task Register_ShouldReportEveryBadFieldAtOnce_SoTheFormCanMarkThemAll()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();

        // Act
        var response = await client.PostAsync(
            "/api/v1/users",
            new { email = "not-an-address", displayName = "  ", password = "short" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = response.Json!.Value.GetProperty("errors").EnumerateArray()
            .Select(cause => cause.GetProperty("field").GetString())
            .ToList();
        Assert.Equal(["email", "displayName", "password"], fields);
    }

    [Fact]
    public async Task Register_ShouldRejectAShortPassword_AtTheBoundary()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();

        // Act
        var response = await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = new string('a', 11) },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("users.weak_password", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_ShouldNeverEchoThePassword_InAnyForm()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();
        const string password = "correct horse battery staple";

        // Act
        var response = await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password },
            Token);

        // Assert
        Assert.DoesNotContain(password, response.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("argon2", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_ShouldNotRequireACsrfToken_BecauseThereIsNoSessionYet()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var client = postgres.Api.NewApiClient();

        // Act
        var response = await client.PostAsync("/api/v1/users", Body("ada@example.com"), Token);

        // Assert
        // The CSRF guard applies to cookie-authenticated requests; sign-up has
        // no ambient credential to abuse.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static object Body(string email) => new
    {
        email,
        displayName = "Ada",
        password = "correct horse battery staple"
    };
}
