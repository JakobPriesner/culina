using Application.Abstractions;
using Domain.Planning;
using Domain.Recipes;
using Domain.Shared;
using Domain.Shopping;
using Infrastructure.Persistence.Planning;

namespace Infrastructure.Persistence.Shopping;

/// <summary>The <c>shopping_list_items</c> row.</summary>
internal sealed record ShoppingItemRow
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public decimal? Quantity { get; init; }

    public string? Unit { get; init; }

    public string Section { get; init; } = string.Empty;

    public bool IsChecked { get; init; }

    public DateTimeOffset? CheckedAt { get; init; }

    public int SortOrder { get; init; }

    public bool IsManual { get; init; }
}

/// <summary>The <c>shopping_list_item_sources</c> row.</summary>
internal sealed record ShoppingSourceRow
{
    public Guid ItemId { get; init; }

    public Guid RecipeId { get; init; }

    public string RecipeTitle { get; init; } = string.Empty;

    public Guid? PlanEntryId { get; init; }

    public DateOnly? PlannedDate { get; init; }

    public string? PlannedSlot { get; init; }

    public decimal? Quantity { get; init; }

    public string? Unit { get; init; }
}

/// <summary>The <c>shopping_lists</c> row.</summary>
internal sealed record ShoppingListRow
{
    public Guid Id { get; init; }

    public Guid HouseholdId { get; init; }

    public long Version { get; init; }
}

/// <summary>The shopping list, in PostgreSQL.</summary>
internal sealed class ShoppingListRepository(DbExecutor executor) : IShoppingListRepository
{
    public async Task<Result<ShoppingList>> ForHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        // Created on first look, and idempotent: two devices opening the list at
        // the same moment must not produce two lists, and the unique index on
        // household_id is what guarantees it.
        var row = await executor.QuerySingleOrDefaultAsync<ShoppingListRow>(
            """
            insert into shopping_lists (id, household_id)
            values (gen_random_uuid(), @householdId)
            on conflict (household_id) do update set household_id = excluded.household_id
            returning id, household_id, version;
            """,
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            return ShoppingErrors.ItemNotFound;
        }

        var items = await executor.QueryAsync<ShoppingItemRow>(
            """
            select id, name, quantity, unit, section, is_checked, checked_at, sort_order, is_manual
            from shopping_list_items
            where list_id = @listId
            order by section, sort_order;
            """,
            new { listId = row.Id },
            cancellationToken).ConfigureAwait(false);

        // Only recipes still in the household's library, as the meal plan reads
        // them: a line keeps its amount when the inheritance behind one of its
        // recipes is cut, but no longer names a recipe nobody here can read.
        // A binned recipe has always dropped out the same way, through the view.
        var sources = await executor.QueryAsync<ShoppingSourceRow>(
            """
            select s.item_id, s.recipe_id, r.title as recipe_title,
                   s.plan_entry_id, p.on_date as planned_date, p.slot as planned_slot,
                   s.quantity, s.unit
            from shopping_list_item_sources s
            join shopping_list_items i on i.id = s.item_id
            join recipes r on r.id = s.recipe_id
            left join meal_plan_entries p on p.id = s.plan_entry_id
            where i.list_id = @listId
              and r.household_id = any(array(select household_library(@householdId)));
            """,
            new { listId = row.Id, householdId },
            cancellationToken).ConfigureAwait(false);

        var sourcesByItem = sources.ToLookup(source => source.ItemId);

        return items
            .Select(item => ToItem(item, sourcesByItem[item.Id]))
            .Collect()
            .Map(restored => ShoppingList.Rehydrate(row.Id, row.HouseholdId, restored, row.Version));
    }

    public async Task<Result> SaveAsync(
        ShoppingList list,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(list);

        // The version is in the WHERE, so zero rows means somebody else changed
        // the list first — never a read-then-write race.
        var changed = await executor.ExecuteAsync(
            """
            update shopping_lists set version = @version
            where id = @id and version = @expectedVersion;
            """,
            new { id = list.Id, version = list.Version, expectedVersion },
            cancellationToken).ConfigureAwait(false);

        if (changed == 0)
        {
            return ConcurrencyErrors.VersionMismatch;
        }

        // Replaced wholesale. The list is small, it is always read and written
        // whole, and a diff would be more code to get subtly wrong than it
        // would ever save.
        await executor.ExecuteAsync(
            "delete from shopping_list_items where list_id = @listId;",
            new { listId = list.Id },
            cancellationToken).ConfigureAwait(false);

        // Two statements whatever the list size: every tick lands here.
        var items = list.Items;

        await executor.ExecuteAsync(
            """
            insert into shopping_list_items
                (id, list_id, name, name_key, quantity, unit, section,
                 is_checked, checked_at, sort_order, is_manual)
            select id, @listId, name, name_key, quantity, unit, section,
                   is_checked, checked_at, sort_order, is_manual
            from unnest(
                @ids::uuid[], @names::text[], @nameKeys::text[], @quantities::numeric[], @units::text[],
                @sections::text[], @checked::boolean[], @checkedAts::timestamptz[], @sortOrders::integer[],
                @manual::boolean[])
                as item(id, name, name_key, quantity, unit, section,
                        is_checked, checked_at, sort_order, is_manual);
            """,
            new
            {
                listId = list.Id,
                ids = items.Select(item => item.Id).ToArray(),
                names = items.Select(item => item.Name.Value).ToArray(),
                nameKeys = items.Select(item => item.Name.ComparisonKey).ToArray(),
                quantities = items.Select(item => item.Quantity.Amount).ToArray(),
                units = items.Select(item => ShoppingWords.Of(item.Quantity.Unit)).ToArray(),
                sections = items.Select(item => ShoppingWords.Of(item.Section)).ToArray(),
                @checked = items.Select(item => item.IsChecked).ToArray(),
                checkedAts = items.Select(item => item.CheckedAt).ToArray(),
                sortOrders = items.Select(item => item.SortOrder).ToArray(),
                manual = items.Select(item => item.IsManual).ToArray()
            },
            cancellationToken).ConfigureAwait(false);

        var sources = items.SelectMany(item => item.Sources.Select(source => (item, source))).ToArray();

        if (sources.Length > 0)
        {
            await executor.ExecuteAsync(
                """
                insert into shopping_list_item_sources (item_id, recipe_id, plan_entry_id, quantity, unit)
                select item_id, recipe_id, plan_entry_id, quantity, unit
                from unnest(
                    @itemIds::uuid[], @recipeIds::uuid[], @planEntryIds::uuid[], @quantities::numeric[],
                    @units::text[])
                    as source(item_id, recipe_id, plan_entry_id, quantity, unit);
                """,
                new
                {
                    itemIds = sources.Select(one => one.item.Id).ToArray(),
                    recipeIds = sources.Select(one => one.source.RecipeId).ToArray(),
                    planEntryIds = sources.Select(one => one.source.PlanEntryId).ToArray(),
                    quantities = sources.Select(one => one.source.Quantity.Amount).ToArray(),
                    units = sources.Select(one => ShoppingWords.Of(one.source.Quantity.Unit)).ToArray()
                },
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<IReadOnlyDictionary<string, ShoppingSection>> SectionOverridesAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync<SectionOverrideRow>(
            "select name_key, section from shopping_section_overrides where household_id = @householdId;",
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        return rows
            .Select(row => (row.NameKey, Section: ShoppingWords.ToSection(row.Section)))
            .Where(pair => pair.Section is not null)
            .ToDictionary(pair => pair.NameKey, pair => pair.Section!.Value, StringComparer.Ordinal);
    }

    public async Task<Result> RememberSectionAsync(
        Guid householdId,
        string nameKey,
        ShoppingSection section,
        CancellationToken cancellationToken)
    {
        await executor.ExecuteAsync(
            """
            insert into shopping_section_overrides (household_id, name_key, section)
            values (@householdId, @nameKey, @section)
            on conflict (household_id, name_key) do update set section = excluded.section;
            """,
            new { householdId, nameKey, section = ShoppingWords.Of(section) },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static Result<ShoppingListItem> ToItem(ShoppingItemRow row, IEnumerable<ShoppingSourceRow> sources) =>
        ItemName.Create(row.Name).Bind(name =>
            Quantity.Create(row.Quantity, ShoppingWords.ToUnit(row.Unit)).Bind(quantity =>
                sources.Select(ToSource).Collect().Map(restored =>
                    ShoppingListItem.Rehydrate(
                        row.Id,
                        name,
                        quantity,
                        ShoppingWords.ToSection(row.Section) ?? ShoppingSection.Other,
                        row.IsChecked,
                        row.CheckedAt,
                        row.SortOrder,
                        row.IsManual,
                        restored))));

    private static Result<ShoppingItemSource> ToSource(ShoppingSourceRow row) =>
        Quantity.Create(row.Quantity, ShoppingWords.ToUnit(row.Unit))
            .Map(quantity => new ShoppingItemSource(
                row.RecipeId,
                row.RecipeTitle,
                row.PlanEntryId,
                row.PlannedDate,
                row.PlannedSlot is null ? null : PlanningCodes.ToSlot(row.PlannedSlot),
                quantity));

    private sealed record SectionOverrideRow
    {
        public string NameKey { get; init; } = string.Empty;

        public string Section { get; init; } = string.Empty;
    }
}
