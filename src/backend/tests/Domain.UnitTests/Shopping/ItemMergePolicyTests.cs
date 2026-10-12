using Domain.Ingredients;
using Domain.Recipes;
using Domain.Shopping;

namespace Domain.UnitTests.Shopping;

/// <summary>The merge rule is the substance of the shopping list.</summary>
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
        var first = Name(left);
        var second = Name(right);

        var matched = ItemMergePolicy.NamesMatch(first, second);

        Assert.True(matched);
    }

    [Theory]
    [InlineData("Tomate", "Tomaten")]
    [InlineData("Apfel", "Äpfel")]
    [InlineData("Spargel, grün", "grüner Spargel")]
    [InlineData("große Zwiebel", "Zwiebeln")]
    [InlineData("gehackte Zwiebeln", "Zwiebel")]
    public void NamesMatch_ShouldMergeOneThingWrittenOtherwise(string left, string right)
    {
        Assert.True(ItemMergePolicy.NamesMatch(Name(left), Name(right)));
    }

    [Theory]
    [InlineData("Butter", "Buttermilch")]
    [InlineData("Mehl", "Vollkornmehl")]
    [InlineData("grüner Spargel", "weißer Spargel")]
    [InlineData("grüner Spargel", "Spargel")]
    [InlineData("Kokosmilch light", "Kokosmilch")]
    [InlineData("gehackte Tomaten", "Tomaten")]
    public void NamesMatch_ShouldNotMergeDifferentThings(string left, string right)
    {
        var matched = ItemMergePolicy.NamesMatch(Name(left), Name(right));

        Assert.False(matched);
    }

    [Fact]
    public void CanMerge_ShouldCombineTheSameFamily()
    {
        var existing = Item("Butter", 200, Unit.Gram);

        var merged = ItemMergePolicy.CanMerge(existing, Name("butter"), Amount(0.05m, Unit.Kilogram));

        Assert.True(merged);
    }

    [Fact]
    public void CanMerge_ShouldRefuseSpoonsAgainstMillilitres()
    {
        var existing = Item("Olivenöl", 2, Unit.Tablespoon);

        var merged = ItemMergePolicy.CanMerge(existing, Name("olivenoel"), Amount(30, Unit.Millilitre));

        // Tablespoons differ by country; converting would invent precision.
        Assert.False(merged);
    }

    [Fact]
    public void CanMerge_ShouldCombineTwoThingsWithNoAmount()
    {
        var existing = Item("Salz", null, null);

        var merged = ItemMergePolicy.CanMerge(existing, Name("salz"), Quantity.Unmeasured);

        Assert.True(merged);
    }

    [Fact]
    public void Add_ShouldSumUnrounded()
    {
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Mehl"), Asked(333.333m, Unit.Gram), ShoppingSection.DryGoods);
        list.Add(Name("mehl"), Asked(333.333m, Unit.Gram), ShoppingSection.DryGoods);
        list.Add(Name("MEHL"), Asked(333.334m, Unit.Gram), ShoppingSection.DryGoods);

        // Rounding first compounds error; rounding is presentation and belongs to the client.
        var item = Assert.Single(list.Items);

        Assert.Equal(1000m, item.Quantity.Amount);
    }

    [Fact]
    public void Add_ShouldConvertIntoTheCanonicalUnit()
    {
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Butter"), Asked(200, Unit.Gram), ShoppingSection.DairyEggs);
        list.Add(Name("butter"), Asked(0.05m, Unit.Kilogram), ShoppingSection.DairyEggs);

        var item = Assert.Single(list.Items);

        Assert.Equal(250m, item.Quantity.Amount);
        Assert.Equal(Unit.Gram, item.Quantity.Unit);
    }

    [Fact]
    public void FitNames_ShouldWriteTheSumInTheHouseholdsOwnWords()
    {
        var list = ShoppingList.Create(Guid.CreateVersion7());
        var maultasche = IngredientNouns.Own("Maultasche", "Maultaschen").Match(noun => noun, _ => null!);

        list.Add(Name("Maultasche"), Asked(1, null), ShoppingSection.Other);
        list.Add(Name("Maultasche"), Asked(2, null), ShoppingSection.Other);
        var before = list.Version;

        list.FitNames([maultasche]);

        var item = Assert.Single(list.Items);
        Assert.Equal("Maultaschen", item.Name.Value);
        Assert.Equal(before + 1, list.Version);
    }

    [Fact]
    public void FitNames_ShouldLeaveTheVersion_WhenNoNameChanges()
    {
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Tomaten"), Asked(3, null), ShoppingSection.Produce);
        var before = list.Version;

        list.FitNames([]);

        Assert.Equal(before, list.Version);
    }

    [Fact]
    public void Add_ShouldWriteTheSumInItsNumber()
    {
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Tomate"), Asked(1, null), ShoppingSection.Produce);
        list.Add(Name("Tomaten"), Asked(3, null), ShoppingSection.Produce);

        var item = Assert.Single(list.Items);

        Assert.Equal(4m, item.Quantity.Amount);
        Assert.Equal("Tomaten", item.Name.Value);
    }

    [Fact]
    public void Add_ShouldWriteADoubledRecipesCountInItsNumber()
    {
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Zwiebel"), Asked(2, null), ShoppingSection.Produce);

        Assert.Equal("Zwiebeln", Assert.Single(list.Items).Name.Value);
    }

    [Fact]
    public void Add_ShouldPutAnotherKindUnderTheFirst_NotWhereverItArrives()
    {
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("grüner Spargel"), Asked(500, Unit.Gram), ShoppingSection.Produce);
        list.Add(Name("Zwiebeln"), Asked(2, null), ShoppingSection.Produce);
        list.Add(Name("weißer Spargel"), Asked(1, Unit.Kilogram), ShoppingSection.Produce);

        // Two kinds of asparagus, side by side and not summed.
        Assert.Equal(
            ["grüner Spargel", "weißer Spargel", "Zwiebeln"],
            list.Items.OrderBy(item => item.SortOrder).Select(item => item.Name.Value));
    }

    [Fact]
    public void Add_ShouldStartANewLine_WhenTheUnitsCannotCombine()
    {
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Olivenöl"), Asked(2, Unit.Tablespoon), ShoppingSection.SpicesBaking);
        list.Add(Name("olivenoel"), Asked(30, Unit.Millilitre), ShoppingSection.SpicesBaking);

        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void Add_ShouldNotMergeIntoSomethingAlreadyInTheTrolley()
    {
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Butter"), Asked(200, Unit.Gram), ShoppingSection.DairyEggs);
        list.Check(list.Items[0].Id, isChecked: true, DateTimeOffset.UnixEpoch);

        list.Add(Name("butter"), Asked(50, Unit.Gram), ShoppingSection.DairyEggs);

        // A ticked line is already bought, so it is not merged into.
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void ClearChecked_ShouldRemoveOnlyWhatIsInTheTrolley()
    {
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Butter"), Asked(200, Unit.Gram), ShoppingSection.DairyEggs);
        list.Add(Name("Mehl"), Asked(500, Unit.Gram), ShoppingSection.DryGoods);
        list.Check(list.Items[0].Id, isChecked: true, DateTimeOffset.UnixEpoch);

        var removed = list.ClearChecked();

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
