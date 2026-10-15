using System.Net;
using System.Net.Http.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Products;

[Collection(RequiresDatabase.Name)]
public class ProductMemoryTests(PostgresFixture postgres)
{
    private const string Barilla = "8076800195057";
    private const string Pesto = "4008400402222";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Put_ShouldNameABarcode_AndGetShouldReadItBackWithTheSameETag()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var put = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti());
        var read = await kitchen.Client.GetAsync(Path(kitchen.HouseholdId, Barilla), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, put.StatusCode);
        Assert.Equal(Path(kitchen.HouseholdId, Barilla), put.Headers.Location!.OriginalString);
        Assert.Equal(put.ETag, read.ETag);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var product = read.Json!.Value;
        Assert.Equal(Barilla, product.GetProperty("gtin").GetString());
        Assert.Equal("Barilla Spaghetti n.5", product.GetProperty("name").GetString());
        Assert.Equal("Spaghetti", product.GetProperty("ingredient").GetString());
        Assert.Equal(500m, product.GetProperty("quantity").GetDecimal());
        Assert.Equal("g", product.GetProperty("unit").GetString());
        Assert.Equal(9, product.GetProperty("cookMinutes").GetInt32());
        Assert.Equal(1, product.GetProperty("version").GetInt64());
        Assert.Equal(kitchen.HouseholdId, product.GetProperty("householdId").GetGuid());
    }

    [Fact]
    public async Task Get_ShouldAnswer304_WhenTheCallerHoldsTheCurrentETag()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var put = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti());

        // Act
        var same = await SendAsync(kitchen.Client, HttpMethod.Get, Path(kitchen.HouseholdId, Barilla), ifNoneMatch: put.ETag);
        var changed = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti(cookMinutes: 10), put.ETag);
        var stale = await SendAsync(kitchen.Client, HttpMethod.Get, Path(kitchen.HouseholdId, Barilla), ifNoneMatch: put.ETag);

        // Assert
        Assert.Equal(HttpStatusCode.NotModified, same.StatusCode);
        Assert.Equal(put.ETag, same.ETag);
        Assert.Empty(same.Body);
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        Assert.Equal(changed.ETag, stale.ETag);
    }

    [Fact]
    public async Task Put_ShouldNormaliseTheBarcode_SoAUpcAAndItsEan13AreOneRow()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var put = await PutAsync(kitchen.Client, kitchen.HouseholdId, "042100005264", Spaghetti());
        var asEan = await kitchen.Client.GetAsync(Path(kitchen.HouseholdId, "0042100005264"), Token);
        var asUpcE = await kitchen.Client.GetAsync(Path(kitchen.HouseholdId, "04252614"), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, put.StatusCode);
        Assert.Equal("0042100005264", put.Json!.Value.GetProperty("gtin").GetString());
        Assert.Equal(HttpStatusCode.OK, asEan.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asUpcE.StatusCode);
        Assert.Equal(asEan.Json!.Value.GetProperty("name").GetString(), asUpcE.Json!.Value.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Put_ShouldAskForTheVersion_WhenTheHouseholdAlreadyNamedTheBarcode()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti());

        // Act: a second phone naming the same code, never having read it.
        var again = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti("Other name"));
        var read = await kitchen.Client.GetAsync(Path(kitchen.HouseholdId, Barilla), Token);

        // Assert
        Assert.Equal(HttpStatusCode.PreconditionRequired, again.StatusCode);
        Assert.Equal("products.already_named", again.ProblemCode);
        Assert.Equal("Barilla Spaghetti n.5", read.Json!.Value.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Put_ShouldChangeTheRow_WhenItCarriesTheVersionThatWasRead()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var created = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti());

        // Act
        var changed = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti(cookMinutes: 11), created.ETag);

        // Assert
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.NotEqual(created.ETag, changed.ETag);
        Assert.Equal(2, changed.Json!.Value.GetProperty("version").GetInt64());
        Assert.Equal(11, changed.Json!.Value.GetProperty("cookMinutes").GetInt32());
    }

    [Fact]
    public async Task Put_ShouldBeRefused_WhenTheVersionIsStale_AndShouldBeNotFound_WhenTheRowIsGone()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var created = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti());
        await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti(cookMinutes: 10), created.ETag);
        var gone = await PutAsync(kitchen.Client, kitchen.HouseholdId, Pesto, Spaghetti("Pesto"));
        await SendAsync(kitchen.Client, HttpMethod.Delete, Path(kitchen.HouseholdId, Pesto), ifMatch: gone.ETag);

        // Act
        var stale = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti(cookMinutes: 12), created.ETag);
        var missing = await PutAsync(kitchen.Client, kitchen.HouseholdId, Pesto, Spaghetti("Pesto"), gone.ETag);
        var malformed = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti(), "\"nonsense\"");
        var read = await kitchen.Client.GetAsync(Path(kitchen.HouseholdId, Barilla), Token);

        // Assert
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("request.version_mismatch", stale.ProblemCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("products.unknown", missing.ProblemCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(10, read.Json!.Value.GetProperty("cookMinutes").GetInt32());
    }

    [Fact]
    public async Task Put_ShouldRefuseWhatIsNotAProduct_SayingWhichPartIsWrong()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var badCode = await PutAsync(kitchen.Client, kitchen.HouseholdId, "8076800195058", Spaghetti());
        var noName = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti(" "));
        var badTime = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti(cookMinutes: 601));
        var badQuantity = await PutAsync(
            kitchen.Client, kitchen.HouseholdId, Barilla, new { name = "Pasta", quantity = 0, unit = "g" });
        var letters = await kitchen.Client.GetAsync(Path(kitchen.HouseholdId, "not-a-code"), Token);

        // Assert
        Assert.Equal("products.invalid_gtin", badCode.ProblemCode);
        Assert.Equal("products.invalid_name", noName.ProblemCode);
        Assert.Equal("products.invalid_cook_minutes", badTime.ProblemCode);
        Assert.Equal("products.invalid_quantity", badQuantity.ProblemCode);
        Assert.Equal(HttpStatusCode.BadRequest, letters.StatusCode);
        Assert.All(
            new[] { badCode, noName, badTime, badQuantity, letters },
            response => Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode));
    }

    [Fact]
    public async Task Get_ShouldBeNotFound_ForABarcodeNobodyNamed()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var read = await kitchen.Client.GetAsync(Path(kitchen.HouseholdId, Barilla), Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal("products.unknown", read.ProblemCode);
    }

    [Fact]
    public async Task Delete_ShouldRequireTheVersion_AndForgetTheRow()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var created = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti());
        var changed = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti(cookMinutes: 10), created.ETag);

        // Act
        var without = await kitchen.Client.DeleteAsync(Path(kitchen.HouseholdId, Barilla), Token);
        var stale = await kitchen.Client.DeleteAsync(Path(kitchen.HouseholdId, Barilla), created.ETag!, Token);
        var removed = await kitchen.Client.DeleteAsync(Path(kitchen.HouseholdId, Barilla), changed.ETag!, Token);
        var again = await kitchen.Client.DeleteAsync(Path(kitchen.HouseholdId, Barilla), changed.ETag!, Token);
        var read = await kitchen.Client.GetAsync(Path(kitchen.HouseholdId, Barilla), Token);

        // Assert
        Assert.Equal(HttpStatusCode.PreconditionRequired, without.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task Get_ShouldReadTheNearestHouseholdsMemory_AndPutShouldLeaveTheAncestorAlone()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var flat = await HeirAsync(kitchen);
        var parent = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti());

        // Act
        var inherited = await kitchen.Client.GetAsync(Path(flat, Barilla), Token);
        var own = await PutAsync(kitchen.Client, flat, Barilla, Spaghetti("Our spaghetti", cookMinutes: 8));
        var nearest = await kitchen.Client.GetAsync(Path(flat, Barilla), Token);
        var ancestor = await kitchen.Client.GetAsync(Path(kitchen.HouseholdId, Barilla), Token);

        // Assert: the heir read its ancestor's row, named its own without touching it, and now reads that.
        Assert.Equal(kitchen.HouseholdId, inherited.Json!.Value.GetProperty("householdId").GetGuid());
        Assert.Equal(HttpStatusCode.Created, own.StatusCode);
        Assert.Equal(flat, nearest.Json!.Value.GetProperty("householdId").GetGuid());
        Assert.Equal("Our spaghetti", nearest.Json!.Value.GetProperty("name").GetString());
        Assert.Equal(8, nearest.Json!.Value.GetProperty("cookMinutes").GetInt32());
        Assert.Equal(parent.ETag, ancestor.ETag);
        Assert.Equal("Barilla Spaghetti n.5", ancestor.Json!.Value.GetProperty("name").GetString());
        Assert.NotEqual(inherited.ETag, nearest.ETag);
    }

    [Fact]
    public async Task Delete_ShouldLetTheHeirSeeItsAncestorsMemoryAgain_AndNeverTouchTheAncestors()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var flat = await HeirAsync(kitchen);
        await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti());
        var own = await PutAsync(kitchen.Client, flat, Barilla, Spaghetti("Our spaghetti"));

        // Act
        var removedOwn = await kitchen.Client.DeleteAsync(Path(flat, Barilla), own.ETag!, Token);
        var inherited = await kitchen.Client.GetAsync(Path(flat, Barilla), Token);
        var inheritedETag = inherited.ETag!;
        var alreadyGone = await kitchen.Client.DeleteAsync(Path(flat, Barilla), inheritedETag, Token);
        var ancestor = await kitchen.Client.GetAsync(Path(kitchen.HouseholdId, Barilla), Token);

        // Assert: the ancestor's tag, held by the heir, matches no row of the heir's, so nothing it holds is deleted.
        Assert.Equal(HttpStatusCode.NoContent, removedOwn.StatusCode);
        Assert.Equal(kitchen.HouseholdId, inherited.Json!.Value.GetProperty("householdId").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, alreadyGone.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ancestor.StatusCode);
    }

    [Fact]
    public async Task Get_ShouldNotShowAnHeirsMemoryToItsAncestor()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var flat = await HeirAsync(kitchen);
        await PutAsync(kitchen.Client, flat, Barilla, Spaghetti());

        // Act
        var ancestor = await kitchen.Client.GetAsync(Path(kitchen.HouseholdId, Barilla), Token);

        // Assert
        Assert.Equal("products.unknown", ancestor.ProblemCode);
    }

    [Fact]
    public async Task List_ShouldHoldOneEntryPerBarcode_TheNearestHouseholdsFirst_WithAnETagThatFollowsTheRows()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var flat = await HeirAsync(kitchen);
        await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti());
        await PutAsync(kitchen.Client, kitchen.HouseholdId, Pesto, Spaghetti("Pesto Rosso"));
        var own = await PutAsync(kitchen.Client, flat, Barilla, Spaghetti("Our spaghetti"));

        // Act
        var list = await kitchen.Client.GetAsync(ListPath(flat), Token);
        var same = await SendAsync(kitchen.Client, HttpMethod.Get, ListPath(flat), ifNoneMatch: list.ETag);
        await PutAsync(kitchen.Client, flat, Barilla, Spaghetti("Our spaghetti", cookMinutes: 7), own.ETag);
        var after = await SendAsync(kitchen.Client, HttpMethod.Get, ListPath(flat), ifNoneMatch: list.ETag);
        var ancestorList = await kitchen.Client.GetAsync(ListPath(kitchen.HouseholdId), Token);

        // Assert
        var items = list.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(new[] { Pesto, Barilla }.Order(StringComparer.Ordinal), items.Select(item => item.GetProperty("gtin").GetString()));
        Assert.Equal(
            "Our spaghetti",
            items.Single(item => item.GetProperty("gtin").GetString() == Barilla).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NotModified, same.StatusCode);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.NotEqual(list.ETag, after.ETag);
        Assert.Equal(
            "Barilla Spaghetti n.5",
            ancestorList.Json!.Value.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("gtin").GetString() == Barilla).GetProperty("name").GetString());
    }

    [Fact]
    public async Task List_ShouldBeEmpty_BeforeAnythingIsNamed()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var list = await kitchen.Client.GetAsync(ListPath(kitchen.HouseholdId), Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(0, list.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task EveryRoute_ShouldAnswerNotFound_ToSomeoneWhoIsNotInTheHousehold()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var created = await PutAsync(kitchen.Client, kitchen.HouseholdId, Barilla, Spaghetti());
        var stranger = await Kitchen.StrangerAsync(postgres);

        // Act
        var list = await stranger.GetAsync(ListPath(kitchen.HouseholdId), Token);
        var read = await stranger.GetAsync(Path(kitchen.HouseholdId, Barilla), Token);
        var put = await PutAsync(stranger, kitchen.HouseholdId, Pesto, Spaghetti());
        var change = await PutAsync(stranger, kitchen.HouseholdId, Barilla, Spaghetti("Mine"), created.ETag);
        var delete = await stranger.DeleteAsync(Path(kitchen.HouseholdId, Barilla), created.ETag!, Token);
        var unchanged = await kitchen.Client.GetAsync(Path(kitchen.HouseholdId, Barilla), Token);

        // Assert
        Assert.All(
            new[] { list, read, put, change, delete },
            response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        Assert.Equal(created.ETag, unchanged.ETag);
    }

    [Fact]
    public async Task EveryRoute_ShouldRequireASession()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var anonymous = postgres.Api.NewApiClient();

        // Act
        var list = await anonymous.GetAsync(ListPath(kitchen.HouseholdId), Token);
        var read = await anonymous.GetAsync(Path(kitchen.HouseholdId, Barilla), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
    }

    [Fact]
    public async Task Export_ShouldCarryTheHouseholdsOwnMemory_AndRestoreShouldAddItBackWithoutReplacingWhatIsThere()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var flat = await HeirAsync(kitchen);
        var created = await PutAsync(kitchen.Client, flat, Barilla, Spaghetti());
        await PutAsync(kitchen.Client, kitchen.HouseholdId, Pesto, Spaghetti("Pesto Rosso"));
        var exported = await kitchen.Client.GetAsync($"/api/v1/households/{flat}/archive", Token);
        await kitchen.Client.DeleteAsync(Path(flat, Barilla), created.ETag!, Token);
        var renamed = await PutAsync(kitchen.Client, flat, Barilla, Spaghetti("Named again"));

        // Act: restored once into a household that now names the code differently, and once into an empty one.
        var restored = await UploadAsync(kitchen.Client, flat, exported.Body);
        var asNamed = await kitchen.Client.GetAsync(Path(flat, Barilla), Token);
        var other = await kitchen.Client.PostAsync("/api/v1/households", new { name = "Other" }, Token);
        var otherId = other.Json!.Value.GetProperty("householdId").GetGuid();
        var intoEmpty = await UploadAsync(kitchen.Client, otherId, exported.Body);
        var restoredRow = await kitchen.Client.GetAsync(Path(otherId, Barilla), Token);
        var notInherited = await kitchen.Client.GetAsync(Path(otherId, Pesto), Token);

        // Assert: the archive holds the flat's own row and not the one it inherits.
        var products = System.Text.Json.JsonDocument.Parse(exported.Body).RootElement.GetProperty("products").EnumerateArray().ToList();
        var product = Assert.Single(products);
        Assert.Equal(Barilla, product.GetProperty("gtin").GetString());
        Assert.Equal("Barilla Spaghetti n.5", product.GetProperty("name").GetString());
        Assert.Equal(9, product.GetProperty("cookMinutes").GetInt32());

        Assert.Equal(HttpStatusCode.Created, renamed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal("Named again", asNamed.Json!.Value.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.OK, intoEmpty.StatusCode);
        Assert.Equal("Barilla Spaghetti n.5", restoredRow.Json!.Value.GetProperty("name").GetString());
        Assert.Equal(otherId, restoredRow.Json!.Value.GetProperty("householdId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, notInherited.StatusCode);
    }

    [Fact]
    public async Task Restore_ShouldStillTakeAnArchiveWrittenBeforeProductMemory_AndPassOverAProductThatDoesNotHoldTogether()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var old = await UploadAsync(
            kitchen.Client,
            kitchen.HouseholdId,
            """{ "culina": 1, "exportedAt": "2026-09-13T10:00:00+00:00", "recipes": [] }""");
        var odd = await UploadAsync(
            kitchen.Client,
            kitchen.HouseholdId,
            $$"""{ "culina": 1, "exportedAt": "2026-10-13T10:00:00+00:00", "recipes": [], "products": [ { "gtin": "123", "name": "Nope" }, { "gtin": "{{Pesto}}", "name": "" }, { "gtin": "{{Barilla}}", "name": "Pasta" } ] }""");
        var list = await kitchen.Client.GetAsync(ListPath(kitchen.HouseholdId), Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, old.StatusCode);
        Assert.Equal(HttpStatusCode.OK, odd.StatusCode);
        var item = Assert.Single(list.Json!.Value.GetProperty("items").EnumerateArray());
        Assert.Equal(Barilla, item.GetProperty("gtin").GetString());
    }

    private static string Path(Guid householdId, string gtin) => $"/api/v1/households/{householdId}/products/{gtin}";

    private static string ListPath(Guid householdId) => $"/api/v1/households/{householdId}/products";

    private static object Spaghetti(string name = "Barilla Spaghetti n.5", int cookMinutes = 9) =>
        new { name, ingredient = "Spaghetti", quantity = 500, unit = "g", cookMinutes, note = (string?)null };

    private static Task<ApiResponse> PutAsync(ApiClient client, Guid householdId, string gtin, object body, string? ifMatch = null) =>
        SendAsync(client, HttpMethod.Put, Path(householdId, gtin), body, ifMatch);

    private static Task<ApiResponse> SendAsync(
        ApiClient client,
        HttpMethod method,
        string path,
        object? body = null,
        string? ifMatch = null,
        string? ifNoneMatch = null)
    {
        var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (ifNoneMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        }

        return client.SendAsync(request, Token);
    }

    private static async Task<ApiResponse> UploadAsync(ApiClient client, Guid householdId, string archive)
    {
        using var content = new MultipartFormDataContent();
        using var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(archive));

        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        content.Add(file, "file", "culina.json");

        return await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, $"/api/v1/households/{householdId}/archive") { Content = content },
            Token);
    }

    private static async Task<Guid> HeirAsync(Kitchen kitchen) =>
        (await kitchen.Client.PostAsync(
            "/api/v1/households",
            new { name = "Flat", inheritsFrom = kitchen.HouseholdId },
            Token)).Json!.Value.GetProperty("householdId").GetGuid();
}
