using System.Net.Http.Headers;
using System.Text.Json;
using Application.Abstractions.Settings;
using Domain.Import;
using Infrastructure.Import;
using TestSupport;

namespace IntegrationTests.Import;

/// <summary>
/// The connected-source client, against servers it can actually reach.
/// </summary>
/// <remarks>
/// Every server here is on loopback, which is the address no setting lets a
/// deployment reach. Where a test needs the client to get through anyway, it
/// builds one that admits every address, which is how the background import
/// tests reach their fake Tandoor.
/// </remarks>
public class SourceHttpTests
{
    private static readonly StorageSettings Storage = new()
    {
        ImagePath = Path.GetTempPath(),
        DataProtectionKeyPath = Path.GetTempPath()
    };

    private static readonly AuthenticationHeaderValue Token = new("Bearer", "a-token");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("[::ffff:127.0.0.1]")]
    [InlineData("localhost")]
    public async Task GetAsync_ShouldRefuseLoopback_WhenPrivateAddressesAreAllowed(string host)
    {
        // Arrange
        // Allowing private addresses opens the household's own network, and
        // never this machine: loopback is where the admin ports are.
        using var server = new LoopbackServer("{}", contentType: "application/json");
        using var http = new SourceHttp(new ImportSettings { AllowPrivateSourceAddresses = true }, Storage);

        // Act
        var result = await http
            .GetAsync<JsonElement>(new Uri($"http://{host}:{server.Port}/api/recipe/"), Token, Cancellation)
            .ConfigureAwait(true);

        // Assert
        result.ShouldBeFailure(ImportErrors.CouldNotFetch);
        Assert.Equal(0, server.Requests);
    }

    [Theory]
    [InlineData(401, "{}")]
    [InlineData(200, "<html>not json</html>")]
    [InlineData(500, "{}")]
    public async Task GetAsync_ShouldSayOnlyCouldNotFetch_WhenTheServerIsOnAPrivateAddress(int status, string body)
    {
        // Arrange
        // A refused token and an answer that is not JSON would otherwise be
        // told apart, and together they map which services run inside the
        // network and what they speak.
        using var server = new LoopbackServer(body, status, "application/json");
        using var http = new SourceHttp(Storage, admits: _ => true);

        // Act
        var result = await http
            .GetAsync<JsonElement>(new Uri($"http://127.0.0.1:{server.Port}/api/recipe/"), Token, Cancellation)
            .ConfigureAwait(true);

        // Assert
        result.ShouldBeFailure(ImportErrors.CouldNotFetch);
        Assert.Equal(1, server.Requests);
    }

    [Fact]
    public async Task PostFormAsync_ShouldSayOnlyCouldNotFetch_WhenAPrivateServerRefusesTheSignIn()
    {
        // Arrange
        using var server = new LoopbackServer("{}", 400, "application/json");
        using var http = new SourceHttp(Storage, admits: _ => true);
        var form = new Dictionary<string, string>(StringComparer.Ordinal) { ["username"] = "ada" };

        // Act
        var result = await http
            .PostFormAsync<JsonElement>(new Uri($"http://127.0.0.1:{server.Port}/api-token-auth/"), form, Cancellation)
            .ConfigureAwait(true);

        // Assert
        result.ShouldBeFailure(ImportErrors.CouldNotFetch);
    }

    [Fact]
    public async Task GetAsync_ShouldStillReadTheAnswer_WhenTheServerIsOnAPrivateAddress()
    {
        // Arrange
        // Vague about failure, not deaf: a recipe server on the LAN still works.
        using var server = new LoopbackServer("""{"count": 3}""", contentType: "application/json");
        using var http = new SourceHttp(Storage, admits: _ => true);

        // Act
        var result = await http
            .GetAsync<JsonElement>(new Uri($"http://127.0.0.1:{server.Port}/api/recipe/"), Token, Cancellation)
            .ConfigureAwait(true);

        // Assert
        Assert.Equal(3, result.ShouldBeSuccess().GetProperty("count").GetInt32());
    }
}
