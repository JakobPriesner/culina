using Application.Abstractions.Settings;
using Domain.Import;
using Domain.Recipes;
using Domain.Shared;
using Infrastructure.Import;
using Infrastructure.Import.Culina;
using TestSupport;

namespace IntegrationTests.Import;

/// <summary>Reading another Culina, against a server that answers as one does.</summary>
/// <remarks>
/// The fake is on loopback, which the client treats as a private network: every failure there reads as
/// "could not fetch", so these tests prove what is asked and what is made of the answers, not error wording.
/// </remarks>
public sealed class CulinaLibraryTests : IDisposable
{
    private static readonly StorageSettings Storage = new()
    {
        ImagePath = Path.GetTempPath(),
        DataProtectionKeyPath = Path.GetTempPath()
    };

    private readonly SourceHttp http = new(Storage, admits: _ => true);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SignIn_ShouldSignInAsAnotherInstance_AndKeepTheTokenItIsGiven()
    {
        using var culina = FakeCulina.Start();

        var token = await Library().SignInAsync(Address(culina), "ada@example.com", "secret", Token);

        Assert.Equal(FakeCulina.SessionToken, token.ShouldBeSuccess());

        // As client "instance", so the other side answers with a token and lists the session for what it is.
        var signIn = Assert.Single(culina.Requests);
        Assert.Equal("/api/v1/sessions", signIn.Path);
        Assert.Null(signIn.Authorization);
        Assert.Contains("\"client\":\"instance\"", signIn.Body, StringComparison.Ordinal);
        Assert.Contains("\"email\":\"ada@example.com\"", signIn.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignIn_ShouldSayTheOtherInstanceIsTooOld_WhenItGivesNoToken()
    {
        // A Culina from before client "instance" sets a cookie instead.
        using var culina = FakeCulina.Start(oldVersion: true);

        var token = await Library().SignInAsync(Address(culina), "ada@example.com", "secret", Token);

        token.ShouldBeFailure(ImportErrors.SourceTooOld);
    }

    [Fact]
    public async Task Test_ShouldPass_WithAWorkingToken()
    {
        using var culina = FakeCulina.Start();

        var tested = await Library().TestAsync(Source(culina, FakeCulina.SessionToken), Token);

        tested.ShouldBeSuccess();
        Assert.Contains(culina.Requests, request => request.Path == "/api/v1/recipes");
    }

    [Fact]
    public async Task Test_ShouldFail_WithARefusedToken()
    {
        using var culina = FakeCulina.Start();

        var tested = await Library().TestAsync(Source(culina, "not-the-token"), Token);

        tested.ShouldBeFailure(ImportErrors.CouldNotFetch);
    }

    [Fact]
    public async Task Browse_ShouldWalkEveryKitchen_FollowingEachOnesCursor()
    {
        using var culina = FakeCulina.Start();
        var source = Source(culina, FakeCulina.SessionToken);

        List<SourcePage> pages = [];
        string? next = null;

        do
        {
            var page = (await Library().BrowseAsync(source, next, query: null, Token)).ShouldBeSuccess();
            pages.Add(page);
            next = page.NextPage;
        }
        while (next is not null && pages.Count < 10);

        // Home's three over two pages, then Holiday's own; the recipe Home lends Holiday is shown once, at Home.
        Assert.Equal(
            [FakeCulina.IdOf(1), FakeCulina.IdOf(2), FakeCulina.IdOf(3), FakeCulina.IdOf(4)],
            pages.SelectMany(page => page.Recipes).Select(recipe => Guid.Parse(recipe.ExternalId)));
        Assert.Equal(3, pages.Count);

        // Several kitchens have no single total; and the kitchen in the bin is never asked about.
        Assert.All(pages, page => Assert.Null(page.Total));
        Assert.DoesNotContain(culina.Requests, request => request.PathAndQuery.Contains("000000000009", StringComparison.Ordinal));
        Assert.All(
            culina.Requests.Where(request => request.Path == "/api/v1/recipes"),
            request => Assert.Contains("sort=title", request.PathAndQuery, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://evil.example.net/api/v1/recipes")]
    [InlineData("7")]
    [InlineData("-1.2")]
    public async Task Browse_ShouldStartOver_WhenThePageIsNotOneOfItsOwn(string page)
    {
        using var culina = FakeCulina.Start();

        await Library().BrowseAsync(Source(culina, FakeCulina.SessionToken), page, query: null, Token);

        var asked = Assert.Single(culina.Requests, request => request.Path == "/api/v1/recipes");
        Assert.Contains($"householdId={FakeCulina.Home}", asked.PathAndQuery, StringComparison.Ordinal);
        Assert.DoesNotContain("cursor=", asked.PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Browse_ShouldPassTheSearchAlong_AsTyped()
    {
        using var culina = FakeCulina.Start();

        await Library().BrowseAsync(Source(culina, FakeCulina.SessionToken), page: null, query: " Kürbis & Co ", Token);

        var asked = Assert.Single(culina.Requests, request => request.Path == "/api/v1/recipes");
        Assert.Contains("&query=K%C3%BCrbis%20%26%20Co&asTyped=true", asked.PathAndQuery, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Browse_ShouldReadSummaries_WithTheirPicture()
    {
        using var culina = FakeCulina.Start();

        var page = (await Library().BrowseAsync(Source(culina, FakeCulina.SessionToken), null, null, Token))
            .ShouldBeSuccess();

        var first = page.Recipes[0];
        Assert.Equal("Recipe 1", first.Title);
        Assert.Equal(30, first.CookMinutes);
        Assert.Equal($"/api/v1/recipes/{FakeCulina.IdOf(1)}/image?w=1600", first.ImageUrl);
    }

    [Theory]
    [InlineData("recipe-1")]
    [InlineData("../users/me")]
    [InlineData("00000000000000000001000000000001")]
    public async Task Fetch_ShouldRefuseAnythingButAnId_BeforeAskingAtAll(string externalId)
    {
        using var culina = FakeCulina.Start();

        var fetched = await Library().FetchAsync(Source(culina, FakeCulina.SessionToken), externalId, Token);

        fetched.ShouldBeFailure(ImportErrors.SourceNotUnderstood);
        Assert.Empty(culina.Requests);
    }

    [Fact]
    public async Task Fetch_ShouldReadTheWholeRecipe_AsItIsThere()
    {
        using var culina = FakeCulina.Start();

        var recipe = (await Library().FetchAsync(Source(culina, FakeCulina.SessionToken), FakeCulina.IdOf(1).ToString(), Token))
            .ShouldBeSuccess();

        Assert.Equal("Kürbissuppe", recipe.Title);
        Assert.Equal(Language.De, recipe.Language);
        Assert.Equal((6m, YieldKind.Pieces, "Gläser"), (recipe.Servings, recipe.YieldKind, recipe.YieldLabel));
        Assert.Equal((15, 40), (recipe.PrepMinutes, recipe.CookMinutes));
        Assert.Equal(["herbst", "suppe"], recipe.Tags);
        Assert.Equal("https://example.com/kuerbissuppe", recipe.SourceUrl);
        Assert.Equal($"/api/v1/recipes/{FakeCulina.IdOf(1)}/image?w=1600", recipe.ImageUrl);
        Assert.Equal(["Suppe", "Zum Servieren"], recipe.Groups.Select(group => group.Name));
        Assert.Equal(["Kürbis", "Brühe", "Kernöl"], recipe.Ingredients.Select(line => line.Name));
    }

    [Fact]
    public async Task Fetch_ShouldTurnIngredientIdsIntoPositions_AcrossGroups()
    {
        using var culina = FakeCulina.Start();

        var recipe = (await Library().FetchAsync(Source(culina, FakeCulina.SessionToken), FakeCulina.IdOf(1).ToString(), Token))
            .ShouldBeSuccess();

        var cook = recipe.Steps[0];
        Assert.Equal("Kochen", cook.Title);
        Assert.Equal(1200, cook.Seconds);
        Assert.Equal([new SourceIngredientReference(0), new SourceTextSegment(" weich kochen.")], cook.Segments);
        Assert.Equal([1], cook.Uses);

        // Kernöl is the first line of the second group, so third overall; the reference to a line that is not
        // in the recipe is dropped, not guessed.
        Assert.Equal(
            [new SourceTextSegment("Mit "), new SourceIngredientReference(2), new SourceTextSegment(" beträufeln.")],
            recipe.Steps[1].Segments);
    }

    [Fact]
    public async Task FetchPicture_ShouldRefuseAnAddressOffTheConnectionsServer()
    {
        using var culina = FakeCulina.Start();

        var picture = await Library().FetchPictureAsync(
            Source(culina, FakeCulina.SessionToken),
            "https://evil.example.net/image.webp",
            Token);

        picture.ShouldBeFailure(ImportErrors.UnreachableAddress);
        Assert.Empty(culina.Requests);
    }

    public void Dispose() => http.Dispose();

    private CulinaLibrary Library() => new(http);

    private static SourceAddress Address(FakeCulina culina) =>
        SourceAddress.Create(culina.Address).Match(value => value, error => throw new InvalidOperationException(error.Code));

    private static RecipeSource Source(FakeCulina culina, string token) =>
        RecipeSource.Restore(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SourceKind.Culina,
            "culina",
            Address(culina),
            token,
            Guid.NewGuid(),
            DateTimeOffset.UnixEpoch,
            lastUsedAt: null,
            version: 1);
}
