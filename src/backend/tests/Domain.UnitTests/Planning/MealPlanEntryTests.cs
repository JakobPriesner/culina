using Domain.Planning;
using TestSupport;

namespace Domain.UnitTests.Planning;

/// <summary>
/// What a plan will and will not accept.
/// </summary>
public class MealPlanEntryTests
{
    private static readonly DateOnly Thursday = new(2026, 9, 17);

    [Fact]
    public void Plan_ShouldAcceptNoServings_MeaningHoweverManyItWasWrittenFor()
    {
        // Arrange & Act
        var entry = MealPlanEntry
            .Plan(CulinaIdStub, Thursday, CulinaIdStub, servings: null, MealSlot.Dinner, 0)
            .ShouldBeSuccess();

        // Assert
        // Most planned meals are cooked as written, and asking every time is a
        // question with an obvious answer.
        Assert.Null(entry.Servings);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Plan_ShouldRefuseAnImpossibleNumberOfServings(int servings)
    {
        // Arrange & Act
        var result = MealPlanEntry.Plan(
            CulinaIdStub, Thursday, CulinaIdStub, servings, MealSlot.Dinner, 0);

        // Assert
        result.ShouldBeFailure(PlanningErrors.InvalidServings);
    }

    [Fact]
    public void MoveTo_ShouldCarryTheMealRatherThanRePlanIt()
    {
        // Arrange
        var entry = MealPlanEntry
            .Plan(CulinaIdStub, Thursday, CulinaIdStub, servings: 6, MealSlot.Lunch, 3)
            .ShouldBeSuccess();

        // Act
        var moved = entry.MoveTo(Thursday.AddDays(2), MealSlot.Lunch, 0);

        // Assert
        // The same entry on another day: it keeps its id, so a move is one row
        // changing rather than one disappearing and another appearing, and it
        // keeps the servings somebody chose.
        Assert.Equal(entry.Id, moved.Id);
        Assert.Equal(entry.RecipeId, moved.RecipeId);
        Assert.Equal(6m, moved.Servings);
        Assert.Equal(Thursday.AddDays(2), moved.Date);
        Assert.Equal(0, moved.SortOrder);
    }

    [Fact]
    public void MoveTo_ShouldLeaveTheEntryItWasCalledOnAlone()
    {
        // Arrange
        var entry = MealPlanEntry
            .Plan(CulinaIdStub, Thursday, CulinaIdStub, servings: null, MealSlot.Dinner, 0)
            .ShouldBeSuccess();

        // Act
        entry.MoveTo(Thursday.AddDays(1), MealSlot.Breakfast, 1);

        // Assert
        // A copy, not a mutation. An optimistic screen is holding the old one
        // to put back if the move fails, and it has to still say Thursday.
        Assert.Equal(Thursday, entry.Date);
        Assert.Equal(MealSlot.Dinner, entry.Slot);
    }

    private static Guid CulinaIdStub => Guid.Parse("01a09999-0000-7000-8000-000000000001");
}

/// <summary>
/// Which Monday a day belongs to.
/// </summary>
/// <remarks>
/// The whole week view hangs off this, and it is the kind of arithmetic that is
/// right for six days of the week and wrong for the seventh.
/// </remarks>
public class PlanningWeekTests
{
    [Theory]
    // A Monday is its own week's start.
    [InlineData("2026-09-14", "2026-09-14")]
    [InlineData("2026-09-17", "2026-09-14")]
    // The one that catches an off-by-one: Sunday belongs to the week behind it,
    // not the one ahead.
    [InlineData("2026-09-20", "2026-09-14")]
    [InlineData("2026-09-21", "2026-09-21")]
    public void StartOfWeekContaining_ShouldSnapToTheMondayBefore(string day, string expected)
    {
        // Arrange & Act
        var monday = PlanningWeek.StartOfWeekContaining(DateOnly.Parse(day, null));

        // Assert
        Assert.Equal(DateOnly.Parse(expected, null), monday);
    }
}
