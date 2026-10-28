using System.Net;
using System.Text;
using IntegrationTests.Fixtures;
using Infrastructure.Products;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Products;

/// <summary>
/// Open Food Facts behind the admin's switch (<c>docs/app-structure-design.md</c> §6.3), against a scripted stand-in:
/// no test reaches the real openfoodfacts.org.
/// </summary>
[Collection(RequiresDatabase.Name)]
public sealed class OpenFoodFactsTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";
    private const string Skyr = "4002971243802";
    private const string OwnBrand = "4008400402222";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lookup_ShouldStayOnTheServer_WhenTheAdministratorSwitchesItOff()
    {
        // Arrange
        var off = new ScriptedOpenFoodFacts();
        using var world = await OpenAsync(off, switchedOff: true);

        // Act
        var product = await world.Client.GetAsync($"/api/v1/products/{Skyr}", Token);
        var search = await world.Client.GetAsync("/api/v1/products?q=skyr", Token);
        var me = await world.Client.GetAsync("/api/v1/users/me", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, product.StatusCode);
        Assert.Equal("products.catalogue_off", product.ProblemCode);
        Assert.Equal("products.catalogue_off", search.ProblemCode);
        Assert.False(me.Json!.Value.GetProperty("openFoodFacts").GetBoolean());
        Assert.Empty(off.Asked);
    }

    [Fact]
    public async Task Lookup_ShouldBeOnByDefault_NamingCulina_UntilTheAdministratorGivesAContact()
    {
        // Arrange: a new instance, nobody has saved the setting.
        var off = new ScriptedOpenFoodFacts();
        using var world = await OpenAsync(off);

        // Act
        var me = await world.Client.GetAsync("/api/v1/users/me", Token);
        var settings = await world.Client.GetAsync("/api/v1/settings/products", Token);
        var anonymous = await world.Client.GetAsync($"/api/v1/products/{Skyr}", Token);
        var refused = await world.Client.PutAsync(
            "/api/v1/settings/products",
            new { openFoodFacts = true, contact = "admin@example.com\r\nX-Evil: 1" },
            Token);
        var saved = await world.Client.PutAsync(
            "/api/v1/settings/products",
            new { openFoodFacts = true, contact = " admin@example.com " },
            Token);
        var named = await world.Client.GetAsync($"/api/v1/products/{OwnBrand}", Token);

        // Assert
        Assert.True(me.Json!.Value.GetProperty("openFoodFacts").GetBoolean());
        Assert.True(settings.Json!.Value.GetProperty("openFoodFacts").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("admin@example.com", saved.Json!.Value.GetProperty("contact").GetString());
        Assert.Equal("products.not_in_catalogue", named.ProblemCode);

        Assert.Equal(2, off.Asked.Count);
        Assert.Contains("(https://github.com/JakobPriesner/culina)", off.Asked[0].UserAgent, StringComparison.Ordinal);
        Assert.Contains("(admin@example.com)", off.Asked[1].UserAgent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lookup_ShouldAnswerFromOpenFoodFacts_AndAskOnlyOncePerCode()
    {
        // Arrange
        var off = new ScriptedOpenFoodFacts();
        using var world = await OpenAsync(off);

        // Act
        var first = await world.Client.GetAsync($"/api/v1/products/{Skyr}", Token);
        var again = await world.Client.GetAsync($"/api/v1/products/{Skyr}", Token);
        var unknown = await world.Client.GetAsync($"/api/v1/products/{OwnBrand}", Token);
        var unknownAgain = await world.Client.GetAsync($"/api/v1/products/{OwnBrand}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var product = first.Json!.Value;
        Assert.Equal("Skyr Natur", product.GetProperty("name").GetString());
        Assert.Equal("Arla", product.GetProperty("brand").GetString());
        Assert.Equal(450m, product.GetProperty("packGrams").GetDecimal());
        Assert.Equal(63m, product.GetProperty("energyKcal").GetDecimal());
        Assert.Equal(11m, product.GetProperty("protein").GetDecimal());
        Assert.Empty(product.GetProperty("doubts").EnumerateArray());
        Assert.Equal($"https://world.openfoodfacts.org/product/{Skyr}", product.GetProperty("url").GetString());
        Assert.Equal(first.Body, again.Body);

        Assert.Equal("products.not_in_catalogue", unknown.ProblemCode);
        Assert.Equal("products.not_in_catalogue", unknownAgain.ProblemCode);

        Assert.Equal(2, off.Asked.Count);
        Assert.All(off.Asked, asked => Assert.StartsWith("Culina/", asked.UserAgent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Search_ShouldAskOnceForGermanProducts_WithOnlyTheTypedWords()
    {
        // Arrange
        var off = new ScriptedOpenFoodFacts();
        using var world = await OpenAsync(off);

        // Act
        var search = await world.Client.GetAsync("/api/v1/products?q=Skyr%20%22Natur%3A", Token);
        var tooShort = await world.Client.GetAsync("/api/v1/products?q=s", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var items = search.Json!.Value.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(Skyr, items[0].GetProperty("gtin").GetString());
        Assert.Equal("Arla", items[0].GetProperty("brand").GetString());
        Assert.Equal("products.invalid_query", tooShort.ProblemCode);

        // The quote and colon typed are dropped, so they cannot become a filter of the search's own.
        var asked = Assert.Single(off.Asked);
        Assert.StartsWith("https://search.openfoodfacts.org/search?", asked.Url, StringComparison.Ordinal);
        Assert.Contains(
            "q=" + Uri.EscapeDataString("Skyr  Natur countries_tags:\"en:germany\""),
            asked.Url,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TakingAProductOver_ShouldKeepAHouseholdFood_ThatTheNextScanFinds()
    {
        // Arrange
        using var world = await OpenAsync(new ScriptedOpenFoodFacts());
        await world.Client.GetAsync($"/api/v1/products/{Skyr}", Token);

        // Act
        var taken = await world.Client.PostAsync(
            $"/api/v1/households/{world.HouseholdId}/foods",
            new
            {
                name = "Skyr Natur",
                energyKj = 266m,
                energyKcal = 63m,
                fat = 0.2m,
                saturatedFat = 0.1m,
                carbohydrate = 4m,
                sugars = 4m,
                protein = 11m,
                salt = 0.1m,
                gtin = Skyr,
                fromCatalogue = true
            },
            Token);
        var scanned = await world.Client.GetAsync($"/api/v1/households/{world.HouseholdId}/products/{Skyr}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, taken.StatusCode);
        Assert.Equal("openFoodFacts", taken.Json!.Value.GetProperty("source").GetString());
        Assert.Equal(Skyr, taken.Json!.Value.GetProperty("gtin").GetString());

        Assert.Equal(HttpStatusCode.OK, scanned.StatusCode);
        var product = scanned.Json!.Value;
        Assert.Equal("Skyr Natur", product.GetProperty("name").GetString());
        Assert.Equal(450m, product.GetProperty("quantity").GetDecimal());
        Assert.Equal("g", product.GetProperty("unit").GetString());
        Assert.Equal(taken.Json!.Value.GetProperty("code").GetString(), product.GetProperty("food").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ACorrectedValue_ShouldMarkTheFoodAsEdited_AndATypedLabelAsThePackage()
    {
        // Arrange
        using var world = await OpenAsync(new ScriptedOpenFoodFacts());
        await world.Client.GetAsync($"/api/v1/products/{Skyr}", Token);

        // Act
        var corrected = await world.Client.PostAsync(
            $"/api/v1/households/{world.HouseholdId}/foods",
            new { name = "Skyr Natur", energyKcal = 66m, protein = 11m, gtin = Skyr, fromCatalogue = true },
            Token);
        var typed = await world.Client.PostAsync(
            $"/api/v1/households/{world.HouseholdId}/foods",
            new { name = "Pesto der Hausmarke", energyKcal = 480m, gtin = OwnBrand },
            Token);
        var named = await world.Client.GetAsync($"/api/v1/households/{world.HouseholdId}/products/{OwnBrand}", Token);
        var wrongCode = await world.Client.PostAsync(
            $"/api/v1/households/{world.HouseholdId}/foods",
            new { name = "Something", energyKcal = 100m, gtin = "1234" },
            Token);

        // Assert
        Assert.Equal("openFoodFactsEdited", corrected.Json!.Value.GetProperty("source").GetString());
        Assert.Equal("package", typed.Json!.Value.GetProperty("source").GetString());
        Assert.Equal("Pesto der Hausmarke", named.Json!.Value.GetProperty("name").GetString());
        Assert.Equal("products.invalid_gtin", wrongCode.ProblemCode);
    }

    /// <summary>A host whose Open Food Facts is <paramref name="off"/> (on, as a new instance has it), a signed-in administrator, and their household.</summary>
    private async Task<World> OpenAsync(ScriptedOpenFoodFacts off, bool switchedOff = false)
    {
        var factory = new CulinaApiFactory(
            postgres,
            replace: services => services.AddSingleton(
                new OpenFoodFactsHttp(new HttpClient(off) { BaseAddress = OpenFoodFactsHttp.Address }, TimeProvider.System)));
        await postgres.ResetAsync(Token);

        var client = factory.NewApiClient();
        await client.PostAsync("/api/v1/users", new { email = "ada@example.com", displayName = "Ada", password = Password }, Token);
        await client.PostAsync("/api/v1/sessions", new { email = "ada@example.com", password = Password }, Token);

        if (switchedOff)
        {
            var saved = await client.PutAsync("/api/v1/settings/products", new { openFoodFacts = false }, Token);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        }

        var me = (await client.GetAsync("/api/v1/users/me", Token)).Json!.Value;

        return new World(factory, client, me.GetProperty("households")[0].GetProperty("householdId").GetGuid());
    }

    private sealed record World(CulinaApiFactory Factory, ApiClient Client, Guid HouseholdId) : IDisposable
    {
        public void Dispose()
        {
            Client.Dispose();
            Factory.Dispose();
        }
    }

    /// <summary>Open Food Facts as Culina meets it: one Skyr it knows, every other code unknown.</summary>
    private sealed class ScriptedOpenFoodFacts : HttpMessageHandler
    {
        private const string SkyrJson =
            $$"""
            {
              "code": "{{Skyr}}",
              "product_name": "Skyr",
              "product_name_de": "Skyr Natur",
              "brands": "Arla, Arla Foods",
              "quantity": "450 g",
              "product_quantity": "450",
              "product_quantity_unit": "g",
              "serving_quantity": 150,
              "nutriments": {
                "energy-kj_100g": 266,
                "energy-kcal_100g": 63,
                "fat_100g": 0.2,
                "saturated-fat_100g": 0.1,
                "carbohydrates_100g": "4",
                "sugars_100g": 4,
                "proteins_100g": 11,
                "salt_100g": 0.1
              }
            }
            """;

        private readonly Lock gate = new();

        public List<(string Url, string UserAgent)> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;

            lock (gate)
            {
                Asked.Add((url, request.Headers.UserAgent.ToString()));
            }

            var (status, body) = url switch
            {
                _ when url.Contains($"/api/v2/product/{Skyr}", StringComparison.Ordinal) =>
                    (HttpStatusCode.OK, $$"""{"status": 1, "product": {{SkyrJson}}}"""),
                _ when url.StartsWith("https://search.openfoodfacts.org/search", StringComparison.Ordinal) =>
                    (HttpStatusCode.OK, $$"""{"count": 1, "hits": [{{SkyrJson.Replace("\"Arla, Arla Foods\"", "[\"Arla\", \" Arla Foods\"]", StringComparison.Ordinal)}}]}"""),
                _ => (HttpStatusCode.NotFound, """{"status": 0, "status_verbose": "product not found"}""")
            };

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
