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
        // Joined, not fetched per card. Only recipes still in the library: a meal from a cut inheritance drops off
        // the week but the row is kept, so it returns if the inheritance does.
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
              and r.household_id = any(array(select household_library(@householdId)))
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
        // The household is in the where clause: "not yours" and "not there" must answer alike or entry ids leak.
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
                // Doubled so the moved entry lands between two neighbours without renumbering them first.
                key = moved.SortOrder * 2
            },
            cancellationToken).ConfigureAwait(false);

        // Renumbered from zero so later drops aim at gaps where they look. The others count double and odd (ranked before doubling,
        // since a day can have gaps), so the moved entry's even key slots into its gap without a tie.
        await executor.ExecuteAsync(
            """
            with keyed as (
                select id,
                       case when id = @id then sort_order
                            else (row_number() over (partition by id = @id order by sort_order) - 1) * 2 + 1
                       end as sort_key
                from meal_plan_entries
                where household_id = @householdId and on_date = @date
            ),
            ordered as (
                select id, row_number() over (order by sort_key) - 1 as position
                from keyed
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
        // The day comes back from the delete itself, saving a read.
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
        // Only when both are known: a guessed total is a number somebody plans an evening around.
        row.PrepMinutes is { } prep && row.CookMinutes is { } cook ? prep + cook : null,
        row.YieldAmount,
        row.IsOnShoppingList);
}

/// <summary>How a slot is spelled in the database: text, so inserting a slot cannot silently reassign rows.</summary>
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
