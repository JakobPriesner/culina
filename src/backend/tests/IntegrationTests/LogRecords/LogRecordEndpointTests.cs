using System.Net;
using System.Net.Http.Json;
using IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task Create_ShouldRefuseEverybody_OnceAllCallersTogetherReachTheSharedCeiling()
    {
        // Arrange
        using var api = new CulinaApiFactory(postgres);
        using var host = api.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, ClientAddressFromHeader>()));
        using var client = new ApiClient(host.CreateClient());

        // Act
        var accepted = 0;

        for (var caller = 1; caller <= 120; caller++)
        {
            var response = await client.SendAsync(Report(caller), Token);
            accepted += response.StatusCode == HttpStatusCode.Accepted ? 1 : 0;
        }

        var refused = await client.SendAsync(Report(121), Token);

        // Assert
        // One report from each of 121 addresses: none of them near its own
        // limit, and still the last one refused, because a botnet is many
        // addresses and the disk is one.
        Assert.Equal(120, accepted);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal("request.rate_limited", refused.ProblemCode);
    }

    private static HttpRequestMessage Report(int caller)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/log-records")
        {
            Content = JsonContent.Create(Batch("uncaught_error"))
        };

        request.Headers.Add(ClientAddressFromHeader.Header, $"203.0.113.{caller}");

        return request;
    }

    /// <summary>
    /// Lets a test speak from many addresses, which the in-memory test server
    /// otherwise cannot.
    /// </summary>
    private sealed class ClientAddressFromHeader : IStartupFilter
    {
        internal const string Header = "X-Test-Client-Address";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, following) =>
            {
                if (context.Request.Headers.TryGetValue(Header, out var address))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(address.ToString());
                }

                return following(context);
            });

            next(app);
        };
    }

    private static object Batch(string @event) =>
        new
        {
            appVersion = "test",
            client = new
            {
                sessionId = "0198c0de-2222-7000-8000-000000000000",
                languages = new[] { "de-DE", "en" },
                brands = new[] { "Chromium 140" },
                mobile = true,
                viewportWidth = 390,
                pixelRatio = 3.0,
                connection = "4g"
            },
            records = new[]
            {
                new
                {
                    @event,
                    message = "Cannot read properties of undefined (reading 'title')",
                    stack = "at render (http://localhost/_app/immutable/chunks/a.js:1:42)",
                    route = "/(app)/recipes/[recipeId]",
                    occurredAt = "2026-10-02T08:15:00.000Z",
                    pageAge = 1234L,
                    online = true,
                    heapUsed = 12_345_678L
                }
            }
        };
}
