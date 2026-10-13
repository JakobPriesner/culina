using Application.Abstractions.Settings;
using Domain.Import;
using Infrastructure.Import;
using Infrastructure.Import.Mealie;
using TestSupport;

namespace IntegrationTests.Import;

/// <summary>Reading a Mealie, against a server that answers as one does.</summary>
/// <remarks>
/// The fake is on loopback, which the client treats as a private network: every failure there reads as
/// "could not fetch", so these tests prove what is asked and what is made of the answers, not error wording.
/// </remarks>
public sealed class MealieLibraryTests : IDisposable
{
    private static readonly StorageSettings Storage = new()
    {
        ImagePath = Path.GetTempPath(),
        DataProtectionKeyPath = Path.GetTempPath()
    };

    private readonly SourceHttp http = new(Storage, admits: _ => true);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SignIn_ShouldMakeALastingToken_WithTheSessionItWasGiven()
    {
        using var mealie = FakeMealie.Start(recipes: 0);

        var token = await Library().SignInAsync(Address(mealie), "ada", "secret", Token);

        // The lasting token is kept; the session one would stop working in two days.
        Assert.Equal(FakeMealie.ApiToken, token.ShouldBeSuccess());

        var minted = Assert.Single(mealie.Requests, request => request.Path == "/api/users/api-tokens");
        Assert.Equal($"Bearer {FakeMealie.SessionToken}", minted.Authorization);
        Assert.Contains("\"name\":\"Culina\"", minted.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_ShouldSayItIsNotPossible_WhenNoSessionComesBack()
    {
        // A single-sign-on-only instance: the answer is to paste a token.
        using var mealie = FakeMealie.Start(recipes: 0, signInGivesNoToken: true);

        var token = await Library().SignInAsync(Address(mealie), "ada", "secret", Token);

        token.ShouldBeFailure(ImportErrors.SignInNotPossible);
        Assert.DoesNotContain(mealie.Requests, request => request.Path == "/api/users/api-tokens");
    }

    [Fact]
    public async Task Test_ShouldPass_WithAWorkingToken()
    {
        using var mealie = FakeMealie.Start(recipes: 1);

        var tested = await Library().TestAsync(Source(mealie, FakeMealie.ApiToken), Token);

        tested.ShouldBeSuccess();
    }

    [Fact]
    public async Task Test_ShouldFail_WithARefusedToken()
    {
        using var mealie = FakeMealie.Start(recipes: 1);

        var tested = await Library().TestAsync(Source(mealie, "not-the-token"), Token);

        tested.ShouldBeFailure(ImportErrors.CouldNotFetch);
    }

    [Fact]
    public async Task Browse_ShouldAskForPagesByNumber_AndSayWhichComesNext()
    {
        using var mealie = FakeMealie.Start(recipes: 150);
        var source = Source(mealie, FakeMealie.ApiToken);

        var first = (await Library().BrowseAsync(source, page: null, query: null, Token)).ShouldBeSuccess();
        var second = (await Library().BrowseAsync(source, first.NextPage, query: null, Token)).ShouldBeSuccess();

        Assert.Equal(100, first.Recipes.Count);
        Assert.Equal("2", first.NextPage);
        Assert.Equal(150, first.Total);
        Assert.Equal(50, second.Recipes.Count);
        Assert.Null(second.NextPage);

        // In a stable order, or a page could repeat or skip what the last one showed.
        Assert.All(mealie.Requests, request => Assert.Contains("orderBy=name", request.PathAndQuery, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://evil.example.net/api/recipes?page=2")]
    [InlineData("0")]
    [InlineData("-3")]
    public async Task Browse_ShouldStartOver_WhenThePageIsNotAPageNumber(string page)
    {
        using var mealie = FakeMealie.Start(recipes: 1);

        await Library().BrowseAsync(Source(mealie, FakeMealie.ApiToken), page, query: null, Token);

        var asked = Assert.Single(mealie.Requests);
        Assert.StartsWith("/api/recipes?page=1&", asked.PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Browse_ShouldPassTheSearchAlong()
    {
        using var mealie = FakeMealie.Start(recipes: 1);

        await Library().BrowseAsync(Source(mealie, FakeMealie.ApiToken), page: null, query: " Kürbis & Co ", Token);

        Assert.EndsWith("&search=K%C3%BCrbis%20%26%20Co", Assert.Single(mealie.Requests).PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Browse_ShouldReadSummaries_WithTheirPicture()
    {
        using var mealie = FakeMealie.Start(recipes: 1);

        var page = (await Library().BrowseAsync(Source(mealie, FakeMealie.ApiToken), null, null, Token)).ShouldBeSuccess();

        var summary = Assert.Single(page.Recipes);
        Assert.Equal(FakeMealie.IdOf(1), summary.ExternalId);
        Assert.Equal(10, summary.PrepMinutes);
        Assert.Equal($"/api/media/recipes/{FakeMealie.IdOf(1)}/images/original.webp", summary.ImageUrl);
    }

    [Theory]
    [InlineData("recipe-1")]
    [InlineData("../users/self")]
    [InlineData("00000000000000000000000000000001")]
    public async Task Fetch_ShouldRefuseAnythingButAnId_BeforeAskingAtAll(string externalId)
    {
        using var mealie = FakeMealie.Start(recipes: 1);

        var fetched = await Library().FetchAsync(Source(mealie, FakeMealie.ApiToken), externalId, Token);

        fetched.ShouldBeFailure(ImportErrors.SourceNotUnderstood);
        Assert.Empty(mealie.Requests);
    }

    [Fact]
    public async Task Fetch_ShouldReadTheWholeRecipe()
    {
        using var mealie = FakeMealie.Start(recipes: 1);

        var recipe = (await Library().FetchAsync(Source(mealie, FakeMealie.ApiToken), FakeMealie.IdOf(1), Token))
            .ShouldBeSuccess();

        Assert.Equal("Recipe 1", recipe.Title);
        Assert.Equal(["Für die Suppe", "Zum Servieren"], recipe.Groups.Select(group => group.Name));
        Assert.Equal(2, recipe.Steps.Count);
        Assert.Equal("https://example.com/recipe-1", recipe.SourceUrl);
    }

    public void Dispose() => http.Dispose();

    private MealieLibrary Library() => new(http);

    private static SourceAddress Address(FakeMealie mealie) =>
        SourceAddress.Create(mealie.Address).Match(value => value, error => throw new InvalidOperationException(error.Code));

    private static RecipeSource Source(FakeMealie mealie, string token) =>
        RecipeSource.Restore(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SourceKind.Mealie,
            "mealie",
            Address(mealie),
            token,
            Guid.NewGuid(),
            DateTimeOffset.UnixEpoch,
            lastUsedAt: null,
            version: 1);
}
