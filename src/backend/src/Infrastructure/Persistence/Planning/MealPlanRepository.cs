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

    public bool IsOnShoppingList { get; init; }
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
                   r.title, r.image_id, r.prep_minutes, r.cook_minutes, r.yield_amount,
                   exists (
                       select 1 from shopping_list_item_sources s where s.plan_entry_id = p.id
                   ) as is_on_shopping_list
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

    public async Task<Result<MealPlanEntry>> FindAsync(
        Guid entryId,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        // The household is in the where clause rather than checked afterwards:
        // "not yours" and "not there" are the same answer, and answering them
        // differently tells a stranger which entry ids exist.
        var row = await executor.QuerySingleOrDefaultAsync<PlannedRow>(
            """
            select id, household_id, on_date, recipe_id, servings, slot, sort_order
            from meal_plan_entries
            where id = @entryId and household_id = @householdId;
            """,
            new { entryId, householdId },
            cancellationToken).ConfigureAwait(false);

        return row is null
            ? PlanningErrors.EntryNotFound
            : MealPlanEntry.Restore(
                row.Id,
                row.HouseholdId,
                row.OnDate,
                row.RecipeId,
                row.Servings,
                PlanningCodes.ToSlot(row.Slot),
                row.SortOrder);
    }

    public async Task<Result> MoveAsync(MealPlanEntry moved, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moved);

        await executor.ExecuteAsync(
            """
            update meal_plan_entries
            set on_date = @date, slot = @slot, sort_order = @key
            where id = @id and household_id = @householdId;
            """,
            new
            {
                id = moved.Id,
                householdId = moved.HouseholdId,
                date = moved.Date,
                slot = PlanningCodes.Of(moved.Slot),
                // Doubled, so the moved entry can land between two neighbours
                // without either of them being renumbered first. Every other
                // entry on the day is doubled and offset by one below, which
                // puts this exactly where the gap it was dropped into is.
                key = moved.SortOrder * 2
            },
            cancellationToken).ConfigureAwait(false);

        // Renumbered from zero, so the next thing dropped onto this day is
        // aimed at gaps that are still where they look. Everything but the
        // moved entry counts double and odd — the gap above the meal at index
        // j is the even number 2j, which is the key the moved entry was just
        // given, so it slots in there without a tie to break.
        await executor.ExecuteAsync(
            """
            with ordered as (
                select id,
                       row_number() over (
                           order by case when id = @id then sort_order else sort_order * 2 + 1 end)
                           - 1 as position
                from meal_plan_entries
                where household_id = @householdId and on_date = @date
            )
            update meal_plan_entries entry
            set sort_order = ordered.position
            from ordered
            where entry.id = ordered.id and entry.sort_order is distinct from ordered.position;
            """,
            new
            {
                id = moved.Id,
                householdId = moved.HouseholdId,
                date = moved.Date
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
        row.YieldAmount,
        row.IsOnShoppingList);
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
