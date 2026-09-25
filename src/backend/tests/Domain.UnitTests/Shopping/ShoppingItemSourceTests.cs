using Domain.Planning;
using Domain.Recipes;
using Domain.Shopping;

namespace Domain.UnitTests.Shopping;

/// <summary>
/// What a line on the list is a sum of, and what that makes possible.
/// </summary>
/// <remarks>
/// A planned week can only be put on the list twice without doubling if the
/// list knows which meals are already on it, and a meal can only be taken back
/// off if the list knows how much of each line was that meal's.
/// </remarks>
public class ShoppingItemSourceTests
{
    private static readonly Guid Waffles = Guid.CreateVersion7();
    private static readonly Guid Pasta = Guid.CreateVersion7();
    private static readonly Guid Saturday = Guid.CreateVersion7();
    private static readonly Guid Sunday = Guid.CreateVersion7();
    private static readonly DateOnly SaturdayDate = new(2026, 9, 26);
    private static readonly DateOnly SundayDate = SaturdayDate.AddDays(1);

    [Fact]
    public void Add_ShouldRememberEveryRecipeALineIsTheSumOf()
    {
        // Arrange
        var list = ShoppingList.Create(Guid.CreateVersion7());

        // Act
        list.Add(Name("Mehl"), For(Waffles, Saturday, 250), ShoppingSection.DryGoods);
        list.Add(Name("Mehl"), For(Pasta, Sunday, 400), ShoppingSection.DryGoods);

        // Assert
        var line = Assert.Single(list.Items);

        Assert.Equal(650m, line.Quantity.Amount);
        Assert.Equal([Waffles, Pasta], line.Sources.Select(source => source.RecipeId));
    }

    [Fact]
    public void IsShoppedFor_ShouldKnowWhichPlannedMealsAreAlreadyHere()
    {
        // Arrange
        var list = ShoppingList.Create(Guid.CreateVersion7());

        // Act
        list.Add(Name("Mehl"), For(Waffles, Saturday, 250), ShoppingSection.DryGoods);

        // Assert
        Assert.True(list.IsShoppedFor(Saturday));
        Assert.False(list.IsShoppedFor(Sunday));
    }

    [Fact]
    public void CountFor_ShouldTakeARecipeAddedByItself_AsThePlannedMealsShopping()
    {
        // Arrange
        // The waffles went on the list from their recipe, then onto Saturday.
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Mehl"), For(Waffles, planEntryId: null, 250), ShoppingSection.DryGoods);

        // Act
        var counted = list.CountFor(Waffles, Saturday, SaturdayDate, MealSlot.Dinner);

        // Assert
        // Counted, not added again: Saturday is shopped for once.
        Assert.True(counted);
        Assert.True(list.IsShoppedFor(Saturday));
        Assert.Equal(250m, Assert.Single(list.Items).Quantity.Amount);
        Assert.Equal(SaturdayDate, Assert.Single(list.Items).Sources[0].PlannedDate);
    }

    [Fact]
    public void CountFor_ShouldFindNothing_WhenTheRecipeIsOnlyHereForAnotherMeal()
    {
        // Arrange
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Mehl"), For(Waffles, Saturday, 250), ShoppingSection.DryGoods);

        // Act & Assert
        // Waffles twice in a week is two meals, and each needs its own flour.
        Assert.False(list.CountFor(Waffles, Sunday, SundayDate, MealSlot.Dinner));
    }

    [Fact]
    public void Withdraw_ShouldTakeBackExactlyWhatThatMealAskedFor()
    {
        // Arrange
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Mehl"), For(Waffles, Saturday, 250), ShoppingSection.DryGoods);
        list.Add(Name("Mehl"), For(Pasta, Sunday, 400), ShoppingSection.DryGoods);
        list.Add(Name("Milch"), For(Waffles, Saturday, 500, Unit.Millilitre), ShoppingSection.DairyEggs);

        // Act
        var changed = list.Withdraw(Saturday);

        // Assert
        // The flour the pasta still needs stays; the milk only the waffles
        // wanted goes.
        Assert.Equal(2, changed);

        var line = Assert.Single(list.Items);

        Assert.Equal("Mehl", line.Name.Value);
        Assert.Equal(400m, line.Quantity.Amount);
        Assert.False(list.IsShoppedFor(Saturday));
    }

    [Fact]
    public void Withdraw_ShouldLeaveWhatIsAlreadyInTheTrolley()
    {
        // Arrange
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.Add(Name("Milch"), For(Waffles, Saturday, 500, Unit.Millilitre), ShoppingSection.DairyEggs);
        list.Check(list.Items[0].Id, isChecked: true, DateTimeOffset.UnixEpoch);

        // Act
        var changed = list.Withdraw(Saturday);

        // Assert
        // It has been bought. A list that un-bought it would argue with the shop.
        Assert.Equal(0, changed);
        Assert.Single(list.Items);
    }

    [Fact]
    public void Withdraw_ShouldKeepALineSomebodyTyped()
    {
        // Arrange
        var list = ShoppingList.Create(Guid.CreateVersion7());

        list.AddManual(Name("Salz"), Quantity.Unmeasured, ShoppingSection.SpicesBaking);
        list.Add(Name("Salz"), For(Waffles, Saturday, amount: null), ShoppingSection.SpicesBaking);

        // Act
        list.Withdraw(Saturday);

        // Assert
        Assert.Single(list.Items);
    }

    private static ItemName Name(string value) =>
        ItemName.Create(value).Match(name => name, error => throw new InvalidOperationException(error.Code));

    private static ShoppingItemSource For(Guid recipeId, Guid? planEntryId, decimal? amount, Unit? unit = null) =>
        new(
            recipeId,
            recipeId == Waffles ? "Waffles" : "Pasta",
            planEntryId,
            planEntryId is null ? null : planEntryId == Saturday ? SaturdayDate : SundayDate,
            planEntryId is null ? null : MealSlot.Dinner,
            Quantity.Create(amount, amount is null ? null : unit ?? Unit.Gram)
                .Match(quantity => quantity, error => throw new InvalidOperationException(error.Code)));
}
