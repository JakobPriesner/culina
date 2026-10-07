using Domain.Planning;
using TestSupport;

namespace Domain.UnitTests.Planning;

/// <summary>What a plan will and will not accept.</summary>
public class MealPlanEntryTests
{
    private static readonly DateOnly Thursday = new(2026, 9, 17);

    [Fact]
    public void Plan_ShouldAcceptNoServings_MeaningHoweverManyItWasWrittenFor()
    {
        var entry = MealPlanEntry
            .Plan(CulinaIdStub, Thursday, CulinaIdStub, servings: null, MealSlot.Dinner, 0)
            .ShouldBeSuccess();

        // Most planned meals are cooked as written, so asking every time has an obvious answer.
        Assert.Null(entry.Servings);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Plan_ShouldRefuseAnImpossibleNumberOfServings(int servings)
    {
        var result = MealPlanEntry.Plan(
            CulinaIdStub, Thursday, CulinaIdStub, servings, MealSlot.Dinner, 0);

        result.ShouldBeFailure(PlanningErrors.InvalidServings);
    }

    [Fact]
    public void MoveTo_ShouldCarryTheMealRatherThanRePlanIt()
    {
        var entry = MealPlanEntry
            .Plan(CulinaIdStub, Thursday, CulinaIdStub, servings: 6, MealSlot.Lunch, 3)
            .ShouldBeSuccess();

        var moved = entry.MoveTo(Thursday.AddDays(2), MealSlot.Lunch, 0);

        // The same entry on another day keeps its id (one row changing) and the servings somebody chose.
        Assert.Equal(entry.Id, moved.Id);
        Assert.Equal(entry.RecipeId, moved.RecipeId);
        Assert.Equal(6m, moved.Servings);
        Assert.Equal(Thursday.AddDays(2), moved.Date);
        Assert.Equal(0, moved.SortOrder);
    }

    [Fact]
    public void MoveTo_ShouldLeaveTheEntryItWasCalledOnAlone()
    {
        var entry = MealPlanEntry
            .Plan(CulinaIdStub, Thursday, CulinaIdStub, servings: null, MealSlot.Dinner, 0)
            .ShouldBeSuccess();

        entry.MoveTo(Thursday.AddDays(1), MealSlot.Breakfast, 1);

        // A copy, not a mutation: an optimistic screen holds the old one to put back if the move fails.
        Assert.Equal(Thursday, entry.Date);
        Assert.Equal(MealSlot.Dinner, entry.Slot);
    }

    private static Guid CulinaIdStub => Guid.Parse("01a09999-0000-7000-8000-000000000001");
}

/// <summary>Which Monday a day belongs to: arithmetic that is easily right for six days and wrong for the seventh.</summary>
public class PlanningWeekTests
{
    [Theory]
    // A Monday is its own week's start.
    [InlineData("2026-09-14", "2026-09-14")]
    [InlineData("2026-09-17", "2026-09-14")]
    // The one that catches an off-by-one: Sunday belongs to the week behind it.
    [InlineData("2026-09-20", "2026-09-14")]
    [InlineData("2026-09-21", "2026-09-21")]
    public void StartOfWeekContaining_ShouldSnapToTheMondayBefore(string day, string expected)
    {
        var monday = PlanningWeek.StartOfWeekContaining(DateOnly.Parse(day, null));

        Assert.Equal(DateOnly.Parse(expected, null), monday);
    }
}
