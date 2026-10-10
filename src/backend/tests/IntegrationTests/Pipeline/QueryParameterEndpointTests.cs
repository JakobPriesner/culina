using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Pipeline;

/// <summary>Every endpoint reads a query parameter the same way: a wrong value is <c>request.invalid_parameter</c>, a missing required one <c>request.missing_parameter</c>, and nothing is clamped or coerced.</summary>
[Collection(RequiresDatabase.Name)]
public class QueryParameterEndpointTests(PostgresFixture postgres)
{
    private const string Invalid = "request.invalid_parameter";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Every list endpoint that takes a <c>limit</c>, with the largest value it allows.</summary>
    public static TheoryData<string, int> Lists => new()
    {
        { "/api/v1/recipes?householdId={household}", 100 },
        { "/api/v1/suggestions?householdId={household}", 12 },
        { "/api/v1/cookbooks?householdId={household}", 100 },
        { "/api/v1/recipes/{recipe}/related", 12 }
    };

    public static TheoryData<string> HouseholdScoped => new()
    {
        "/api/v1/recipes?householdId=abc",
        "/api/v1/suggestions?householdId=abc",
        "/api/v1/cookbooks?householdId=abc",
        "/api/v1/tags?householdId=abc",
        "/api/v1/searches?householdId=abc",
        "/api/v1/recipe-sources?householdId=abc"
    };

    [Theory]
    [MemberData(nameof(Lists))]
    public async Task List_ShouldRejectALimitThatIsNotANumber_OrIsOutOfRange_WithTheSameCode(string template, int max)
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen);

        foreach (var limit in new[] { "abc", "0", "-1", (max + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), "1.5", "" })
        {
            var url = Url(template, kitchen, recipe, limit);
            var response = await kitchen.Client.GetAsync(url, Token);

            Assert.True(
                response.StatusCode == HttpStatusCode.BadRequest && response.ProblemCode == Invalid,
                $"{url} answered {(int)response.StatusCode} {response.ProblemCode}");
        }
    }

    [Theory]
    [MemberData(nameof(Lists))]
    public async Task List_ShouldAcceptALimitUpToItsMaximum(string template, int max)
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen);

        var url = Url(template, kitchen, recipe, max.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(HttpStatusCode.OK, (await kitchen.Client.GetAsync(url, Token)).StatusCode);
    }

    [Theory]
    [MemberData(nameof(HouseholdScoped))]
    public async Task HouseholdId_ShouldBeInvalid_WhenItIsNotAnId(string url)
    {
        var kitchen = await Kitchen.OpenAsync(postgres);

        var response = await kitchen.Client.GetAsync(url, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Invalid, response.ProblemCode);
    }

    [Theory]
    [InlineData("/api/v1/recipes")]
    [InlineData("/api/v1/suggestions")]
    [InlineData("/api/v1/cookbooks")]
    [InlineData("/api/v1/tags")]
    [InlineData("/api/v1/searches")]
    [InlineData("/api/v1/recipe-sources")]
    public async Task HouseholdId_ShouldBeMissing_WhenItIsLeftOut(string url)
    {
        var kitchen = await Kitchen.OpenAsync(postgres);

        var response = await kitchen.Client.GetAsync(url, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.missing_parameter", response.ProblemCode);
    }

    [Theory]
    [InlineData("/api/v1/recipes?householdId={household}&maxMinutes=-5")]
    [InlineData("/api/v1/recipes?householdId={household}&maxMinutes=0")]
    [InlineData("/api/v1/suggestions?householdId={household}&maxMinutes=-5")]
    [InlineData("/api/v1/recipes?householdId={household}&cookbookId=abc")]
    [InlineData("/api/v1/recipes?householdId={household}&asTyped=yes")]
    [InlineData("/api/v1/households?deleted=1")]
    public async Task Filters_ShouldBeInvalid_WhenOutOfRangeOrMalformed(string template)
    {
        var kitchen = await Kitchen.OpenAsync(postgres);

        var response = await kitchen.Client.GetAsync(
            template.Replace("{household}", kitchen.HouseholdId.ToString(), StringComparison.Ordinal),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Invalid, response.ProblemCode);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("false")]
    [InlineData("False")]
    public async Task Flags_ShouldBeRead_InAnyCase(string value)
    {
        var kitchen = await Kitchen.OpenAsync(postgres);

        var response = await kitchen.Client.GetAsync(
            $"/api/v1/recipes?householdId={kitchen.HouseholdId}&asTyped={value}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("True", HttpStatusCode.NotFound)]
    [InlineData("TRUE", HttpStatusCode.NotFound)]
    [InlineData("yes", HttpStatusCode.BadRequest)]
    [InlineData("1", HttpStatusCode.BadRequest)]
    public async Task EndSession_ShouldReadCompletedStrictly(string value, HttpStatusCode expected)
    {
        var kitchen = await Kitchen.OpenAsync(postgres);

        // No such session: a well-formed flag gets as far as the 404, a malformed one never does.
        var response = await kitchen.Client.DeleteAsync($"/api/v1/cook-sessions/{Guid.NewGuid()}?completed={value}", Token);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("10/01/2026")]
    [InlineData("1.10.2026")]
    [InlineData("2026-10-1")]
    [InlineData("October 1 2026")]
    [InlineData("2026-13-40")]
    public async Task MealPlan_ShouldRefuseADayThatIsNotIsoFormatted(string from)
    {
        var kitchen = await Kitchen.OpenAsync(postgres);

        var response = await kitchen.Client.GetAsync(
            $"/api/v1/households/{kitchen.HouseholdId}/meal-plan?from={Uri.EscapeDataString(from)}",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Invalid, response.ProblemCode);
    }

    [Fact]
    public async Task MealPlan_ShouldReadAnIsoDay()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);

        var response = await kitchen.Client.GetAsync(
            $"/api/v1/households/{kitchen.HouseholdId}/meal-plan?from=2026-10-01",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RemoveShoppingItems_ShouldRefuseABadItemId_InsteadOfClearingEverything()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);

        var response = await kitchen.Client.DeleteAsync(
            $"/api/v1/households/{kitchen.HouseholdId}/shopping-list/items?itemId=abc",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Invalid, response.ProblemCode);
    }

    private static string Url(string template, Kitchen kitchen, Guid recipe, string limit) =>
        template.Replace("{household}", kitchen.HouseholdId.ToString(), StringComparison.Ordinal)
            .Replace("{recipe}", recipe.ToString(), StringComparison.Ordinal)
        + (template.Contains('?', StringComparison.Ordinal) ? "&" : "?") + $"limit={limit}";

    private static async Task<Guid> RecipeAsync(Kitchen kitchen)
    {
        var created = await kitchen.Client.PostAsync(
            "/api/v1/recipes",
            new { householdId = kitchen.HouseholdId, title = "Linsensuppe" },
            Token);

        return created.Json!.Value.GetProperty("recipeId").GetGuid();
    }
}
