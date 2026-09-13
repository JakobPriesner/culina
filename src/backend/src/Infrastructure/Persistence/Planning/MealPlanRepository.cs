using Application.Abstractions;
using Domain.Planning;
using Domain.Shared;

namespace Infrastructure.Persistence.Planning;

/// <summary>The <c>meal_plan_entries</c> row, joined to its recipe.</summary>
internal sealed record PlannedRow
{
    public Guid Id { get; init; }

    public Guid HouseholdId { get; init; }

    public DateOnly OnDate { get; init; }

    public Guid RecipeId { get; init; }

    public decimal? Servings { get; init; }

    public string Slot { get; init; } = "dinner";

    public int SortOrder { get; init; }

    public string Title { get; init; } = string.Empty;

    public Guid? ImageId { get; init; }

    public int? PrepMinutes { get; init; }

    public int? CookMinutes { get; init; }

    public decimal YieldAmount { get; init; }
}

/// <summary>Stores what a household means to cook.</summary>
/// <param name="executor">Runs the SQL.</param>
internal sealed class MealPlanRepository(DbExecutor executor) : IMealPlanRepository
{
    public async Task<IReadOnlyList<PlannedRecipe>> ForWeekAsync(
        Guid householdId,
        DateOnly from,
        int days,
        CancellationToken cancellationToken)
    {
        // Joined, not fetched per card: a week view is seven cards, and seven
        // round trips is a plan that flickers in one at a time.
        var rows = await executor.QueryAsync<PlannedRow>(
            """
            select p.id, p.household_id, p.on_date, p.recipe_id, p.servings, p.slot, p.sort_order,
                   r.title, r.image_id, r.prep_minutes, r.cook_minutes, r.yield_amount
            from meal_plan_entries p
            join recipes r on r.id = p.recipe_id
            where p.household_id = @householdId
              and p.on_date >= @from
              and p.on_date < @until
            order by p.on_date, p.sort_order;
            """,
            new { householdId, from, until = from.AddDays(days) },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(ToPlanned)];
    }

    public async Task<int> NextSortOrderAsync(
        Guid householdId,
        DateOnly date,
        CancellationToken cancellationToken) =>
        await executor.ExecuteScalarAsync<int>(
            """
            select coalesce(max(sort_order) + 1, 0)
            from meal_plan_entries
            where household_id = @householdId and on_date = @date;
            """,
            new { householdId, date },
            cancellationToken).ConfigureAwait(false);

    public async Task<Result> AddAsync(MealPlanEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await executor.ExecuteAsync(
            """
            insert into meal_plan_entries
                (id, household_id, on_date, recipe_id, servings, slot, sort_order)
            values (@id, @householdId, @date, @recipeId, @servings, @slot, @sortOrder);
            """,
            new
            {
                id = entry.Id,
                householdId = entry.HouseholdId,
                date = entry.Date,
                recipeId = entry.RecipeId,
                servings = entry.Servings,
                slot = PlanningCodes.Of(entry.Slot),
                sortOrder = entry.SortOrder
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<DateOnly>> RemoveAsync(
        Guid entryId,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        // The day comes back from the delete itself. Reading the entry first
        // only to delete it is a round trip to learn what the delete knows.
        var day = await executor.QuerySingleOrDefaultAsync<DateOnly?>(
            """
            delete from meal_plan_entries
            where id = @entryId and household_id = @householdId
            returning on_date;
            """,
            new { entryId, householdId },
            cancellationToken).ConfigureAwait(false);

        return day is null ? PlanningErrors.EntryNotFound : day.Value;
    }

    private static PlannedRecipe ToPlanned(PlannedRow row) => new(
        MealPlanEntry.Restore(
            row.Id,
            row.HouseholdId,
            row.OnDate,
            row.RecipeId,
            row.Servings,
            PlanningCodes.ToSlot(row.Slot),
            row.SortOrder),
        row.Title,
        row.ImageId,
        // Only when both are known: "45 minutes" for a recipe that never said
        // how long it stands is a number somebody would plan an evening around.
        row.PrepMinutes is { } prep && row.CookMinutes is { } cook ? prep + cook : null,
        row.YieldAmount);
}

/// <summary>How a slot is spelled in the database.</summary>
/// <remarks>
/// Text rather than an integer, so a migration that inserts a slot in the
/// middle cannot silently reassign every row — and so a person reading the
/// table can see what it says.
/// </remarks>
internal static class PlanningCodes
{
    internal static string Of(MealSlot slot) => slot switch
    {
        MealSlot.Breakfast => "breakfast",
        MealSlot.Lunch => "lunch",
        _ => "dinner"
    };

    internal static MealSlot ToSlot(string stored) => stored switch
    {
        "breakfast" => MealSlot.Breakfast,
        "lunch" => MealSlot.Lunch,
        _ => MealSlot.Dinner
    };
}
