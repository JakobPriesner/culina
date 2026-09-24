using Application.Abstractions;
using Domain.Recipes;
using Domain.Shared;
using Domain.Shopping;

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

    public Guid? PlanEntryId { get; init; }

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

        var sources = await executor.QueryAsync<ShoppingSourceRow>(
            """
            select s.item_id, s.recipe_id, s.plan_entry_id, s.quantity, s.unit
            from shopping_list_item_sources s
            join shopping_list_items i on i.id = s.item_id
            where i.list_id = @listId;
            """,
            new { listId = row.Id },
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

        foreach (var item in list.Items)
        {
            await executor.ExecuteAsync(
                """
                insert into shopping_list_items
                    (id, list_id, name, name_key, quantity, unit, section,
                     is_checked, checked_at, sort_order, is_manual)
                values
                    (@id, @listId, @name, @nameKey, @quantity, @unit, @section,
                     @isChecked, @checkedAt, @sortOrder, @isManual);
                """,
                new
                {
                    id = item.Id,
                    listId = list.Id,
                    name = item.Name.Value,
                    nameKey = item.Name.ComparisonKey,
                    quantity = item.Quantity.Amount,
                    unit = ShoppingWords.Of(item.Quantity.Unit),
                    section = ShoppingWords.Of(item.Section),
                    isChecked = item.IsChecked,
                    checkedAt = item.CheckedAt,
                    sortOrder = item.SortOrder,
                    isManual = item.IsManual
                },
                cancellationToken).ConfigureAwait(false);

            await InsertSourcesAsync(item, cancellationToken).ConfigureAwait(false);
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

    private async Task InsertSourcesAsync(ShoppingListItem item, CancellationToken cancellationToken)
    {
        foreach (var source in item.Sources)
        {
            await executor.ExecuteAsync(
                """
                insert into shopping_list_item_sources (item_id, recipe_id, plan_entry_id, quantity, unit)
                values (@itemId, @recipeId, @planEntryId, @quantity, @unit);
                """,
                new
                {
                    itemId = item.Id,
                    recipeId = source.RecipeId,
                    planEntryId = source.PlanEntryId,
                    quantity = source.Quantity.Amount,
                    unit = ShoppingWords.Of(source.Quantity.Unit)
                },
                cancellationToken).ConfigureAwait(false);
        }
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
            .Map(quantity => new ShoppingItemSource(row.RecipeId, row.PlanEntryId, quantity));

    private sealed record SectionOverrideRow
    {
        public string NameKey { get; init; } = string.Empty;

        public string Section { get; init; } = string.Empty;
    }
}
