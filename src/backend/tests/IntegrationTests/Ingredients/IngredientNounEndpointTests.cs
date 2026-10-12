using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Ingredients;

[Collection(RequiresDatabase.Name)]
public class IngredientNounEndpointTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Put_ShouldListTheWord_AndAPutAgainChangeItsPlural()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var set = await kitchen.Client.PutAsync(NounPath(kitchen, "Maultasche"), new { many = "Maultaschn" }, Token);
        await kitchen.Client.PutAsync(NounPath(kitchen, "maultasche"), new { many = "Maultaschen" }, Token);
        var listed = await kitchen.Client.GetAsync(NounsPath(kitchen), Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        var noun = Assert.Single(listed.Json!.Value.GetProperty("items").EnumerateArray());
        Assert.Equal("maultasche", noun.GetProperty("one").GetString());
        Assert.Equal("Maultaschen", noun.GetProperty("many").GetString());
    }

    [Fact]
    public async Task Put_ShouldRefuse_APluralThatIsNotAnotherWord()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var set = await kitchen.Client.PutAsync(NounPath(kitchen, "Maultasche"), new { many = "Maultasche" }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, set.StatusCode);
        Assert.Equal("ingredients.invalid_noun", set.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Delete_ShouldForgetTheWord()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await kitchen.Client.PutAsync(NounPath(kitchen, "Maultasche"), new { many = "Maultaschen" }, Token);

        // Act
        var removed = await kitchen.Client.DeleteAsync(NounPath(kitchen, "Maultasche"), Token);
        var listed = await kitchen.Client.GetAsync(NounsPath(kitchen), Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Empty(listed.Json!.Value.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task ShoppingList_ShouldSumAWordInTheHouseholdsPlural()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await kitchen.Client.PutAsync(NounPath(kitchen, "Maultasche"), new { many = "Maultaschen" }, Token);
        await kitchen.Client.PostAsync(ItemsPath(kitchen), new { name = "Maultasche", quantity = 1m }, Token);

        // Act
        var list = await kitchen.Client.PostAsync(ItemsPath(kitchen), new { name = "Maultasche", quantity = 2m }, Token);

        // Assert
        var item = Assert.Single(list.Json!.Value.GetProperty("items").EnumerateArray());
        Assert.Equal("Maultaschen", item.GetProperty("name").GetString());
        Assert.Equal(3m, item.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task Everything_ShouldAnswerNotFound_ForAnotherHousehold()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var stranger = await Kitchen.StrangerAsync(postgres);

        // Act
        var list = await stranger.GetAsync(NounsPath(kitchen), Token);
        var set = await stranger.PutAsync(NounPath(kitchen, "Maultasche"), new { many = "Maultaschen" }, Token);
        var removed = await stranger.DeleteAsync(NounPath(kitchen, "Maultasche"), Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, set.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
    }

    private static string NounsPath(Kitchen kitchen) =>
        $"/api/v1/households/{kitchen.HouseholdId}/ingredient-nouns";

    private static string NounPath(Kitchen kitchen, string one) =>
        $"{NounsPath(kitchen)}/{Uri.EscapeDataString(one)}";

    private static string ItemsPath(Kitchen kitchen) =>
        $"/api/v1/households/{kitchen.HouseholdId}/shopping-list/items";
}
