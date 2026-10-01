using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.LogRecords;

/// <summary>
/// The web app's way of telling the operator what went wrong in it.
/// </summary>
/// <remarks>
/// What is worth proving end to end is the reach of it: a page that broke
/// before anybody signed in has to be able to say so, and a signed-in page is
/// still held to the CSRF rule every other write is.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class LogRecordEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_ShouldAcceptAReport_FromSomebodyNotSignedIn()
    {
        // Arrange
        using var client = postgres.Api.NewApiClient();

        // Act
        var response = await client.PostAsync("/api/v1/log-records", Batch("uncaught_error"), Token);

        // Assert
        // The sign-in page is a page too, and it breaks for the same reasons.
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldAcceptAReport_FromASignedInPage()
    {
        // Arrange
        await postgres.ResetAsync(Token);

        using var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await client.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);

        // Act
        var response = await client.PostAsync("/api/v1/log-records", Batch("csp_violation"), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Create_ShouldRefuseAnEventThatIsNotInTheVocabulary()
    {
        // Arrange
        using var client = postgres.Api.NewApiClient();

        // Act
        var response = await client.PostAsync("/api/v1/log-records", Batch("made_up"), Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static object Batch(string @event) =>
        new
        {
            appVersion = "test",
            records = new[]
            {
                new
                {
                    @event,
                    message = "Cannot read properties of undefined (reading 'title')",
                    stack = "at render (http://localhost/_app/immutable/chunks/a.js:1:42)",
                    route = "/(app)/recipes/[recipeId]"
                }
            }
        };
}
