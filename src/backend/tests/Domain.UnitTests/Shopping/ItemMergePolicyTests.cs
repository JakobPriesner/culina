using Domain.Recipes;
using Domain.Shopping;

namespace Domain.UnitTests.Shopping;

/// <summary>
/// The merge rule is the substance of the shopping list.
/// </summary>
/// <remarks>
/// Adding three recipes and getting "200 g butter", "50 g butter" and
/// "1 tbsp butter" as three lines is how a shopping list stops being worth
/// carrying into a shop.
/// </remarks>
public class ItemMergePolicyTests
{
    [Theory]
    [InlineData("Butter", "butter")]
    [InlineData("Müsli", "Muesli")]
    [InlineData("Muesli", "müsli")]
    [InlineData(" Mehl ", "mehl")]
    [InlineData("Crème fraîche", "creme fraiche")]
    [InlineData("Weiße Bohnen", "weisse bohnen")]
    public void NamesMatch_ShouldSeeThroughCaseAndAccents(string left, string right)
    {
        // Arrange
        var first = Name(left);
        var second = Name(right);

        // Act
        var matched = ItemMergePolicy.NamesMatch(first, second);

        // Assert
        Assert.True(matched);
    }

    [Theory]
    [InlineData("Butter", "Buttermilch")]
    [InlineData("Mehl", "Vollkornmehl")]
    public void NamesMatch_ShouldNotMergeDifferentThings(string left, string right)
    {
        // Arrange & Act
        var matched = ItemMergePolicy.NamesMatch(Name(left), Name(right));

        // Assert
        Assert.False(matched);
    }

    [Fact]
    public void CanMerge_ShouldCombineTheSameFamily()
    {
        // Arrange
        var existing = Item("Butter", 200, Unit.Gram);

        // Act
        var merged = ItemMergePolicy.CanMerge(existing, Name("butter"), Amount(0.05m, Unit.Kilogram));

        // Assert
        Assert.True(merged);
    }

    [Fact]
    public void CanMerge_ShouldRefuseSpoonsAgainstMillilitres()
    {
        // Arrange
        var existing = Item("Olivenöl", 2, Unit.Tablespoon);

        // Act
        var merged = ItemMergePolicy.CanMerge(existing, Name("olivenoel"), Amount(30, Unit.Millilitre));

        // Assert
        // A US tablespoon is 14.8 ml, a metric one 15, an Australian one 20.
        // Converting would invent precision the recipe never had.
        Assert.False(merged);
    }

    [Fact]
    public void CanMerge_ShouldCombineTwoThingsWithNoAmount()
    {
        // Arrange
        var existing = Item("Salz", null, null);

        // Act
        var merged = ItemMergePolicy.CanMerge(existing, Name("salz"), Quantity.Unmeasured);

        // Assert
        // "Salt" and "salt" is salt.
        Assert.True(merged);
    }

    [Fact]
    public void Add_ShouldSumUnrounded()
    {
        // Arrange
        var list = ShoppingList.Create(Guid.CreateVersion7());

        // Act
        // Three recipes each contributing a third of a kilo.
        list.Add(Name("Mehl"), Asked(333.333m, Unit.Gram), ShoppingSection.DryGoods);
        list.Add(Name("mehl"), Asked(333.333m, Unit.Gram), ShoppingSection.DryGoods);
        list.Add(Name("MEHL"), Asked(333.334m, Unit.Gram), ShoppingSection.DryGoods);

        // Assert
        // Rounding first and summing second compounds error; rounding is
        // presentation and belongs to the client.
        var item = Assert.Single(list.Items);

        Assert.Equal(1000m, item.Quantity.Amount);
    }

    [Fact]
    public void Add_ShouldConvertIntoTheCanonicalUnit()
    {
        // Arrange
        var list = ShoppingList.Create(Guid.CreateVersion7());

        // Act
        list.Add(Name("Butter"), Asked(200, Unit.Gram), ShoppingSection.DairyEggs);
        list.Add(Name("butter"), Asked(0.05m, Unit.Kilogram), ShoppingSection.DairyEggs);

        // Assert
        var item = Assert.Single(list.Items);

        Assert.Equal(250m, item.Quantity.Amount);
        Assert.Equal(Unit.Gram, item.Quantity.Unit);
    }

    [Fact]
    public void Add_ShouldStartANewLine_WhenTheUnitsCannotCombine()
    {
        // Arrange
        var list = ShoppingList.Create(Guid.CreateVersion7());

        // Act
        list.Add(Name("Olivenöl"), Asked(2, Unit.Tablespoon), ShoppingSection.SpicesBaking);
        list.Add(Name("olivenoel"), Asked(30, Unit.Millilitre), ShoppingSection.SpicesBaking);

        // Assert
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void Add_ShouldNotMergeIntoSomethingAlreadyInTheTrolley()
    {
        // Arrange
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Butter"), Asked(200, Unit.Gram), ShoppingSection.DairyEggs);
        list.Check(list.Items[0].Id, isChecked: true, DateTimeOffset.UnixEpoch);

        // Act
        list.Add(Name("butter"), Asked(50, Unit.Gram), ShoppingSection.DairyEggs);

        // Assert
        // Adding to it would quietly change an amount somebody has already
        // bought.
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void ClearChecked_ShouldRemoveOnlyWhatIsInTheTrolley()
    {
        // Arrange
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Butter"), Asked(200, Unit.Gram), ShoppingSection.DairyEggs);
        list.Add(Name("Mehl"), Asked(500, Unit.Gram), ShoppingSection.DryGoods);
        list.Check(list.Items[0].Id, isChecked: true, DateTimeOffset.UnixEpoch);

        // Act
        var removed = list.ClearChecked();

        // Assert
        Assert.Equal(1, removed);
        Assert.Equal("Mehl", Assert.Single(list.Items).Name.Value);
    }

    private static ItemName Name(string value) =>
        ItemName.Create(value).Match(name => name, error => throw new InvalidOperationException(error.Code));

    private static Quantity Amount(decimal? amount, Unit? unit) =>
        Quantity.Create(amount, unit).Match(q => q, error => throw new InvalidOperationException(error.Code));

    private static ShoppingItemSource Asked(decimal? amount, Unit? unit) =>
        new(
            Guid.CreateVersion7(),
            "Recipe",
            PlanEntryId: null,
            PlannedDate: null,
            PlannedSlot: null,
            Amount(amount, unit));

    private static ShoppingListItem Item(string name, decimal? amount, Unit? unit) =>
        ShoppingListItem.Asked(Name(name), Asked(amount, unit), ShoppingSection.Other, 1);
}
