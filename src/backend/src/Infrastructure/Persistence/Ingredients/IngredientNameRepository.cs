using Application.Abstractions;
using Domain.Ingredients;
using Domain.Shopping;
using Infrastructure.Persistence.Recipes;

namespace Infrastructure.Persistence.Ingredients;

/// <summary>Reads a household's ingredient names and keeps its merges of them.</summary>
/// <param name="executor">Runs the SQL inside the request's transaction.</param>
/// <param name="documents">Rebuilds the search document of every recipe a merge rewrites.</param>
internal sealed class IngredientNameRepository(DbExecutor executor, SearchDocumentWriter documents)
    : IIngredientNameRepository
{
    // Choices a household made for a name, which should follow it into a merge: each table, and what
    // besides the name tells two of its rows apart.
    private static readonly (string Table, string SameRow)[] ChoicesByName =
    [
        ("nutrition_food_overrides", string.Empty),
        ("nutrition_unit_weights", "and kept.unit_key = moved.unit_key"),
        ("shopping_section_overrides", string.Empty)
    ];

    public async Task<IReadOnlyList<IngredientUse>> UsesAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync<UseRow>(
            """
            select i.name, count(distinct r.id)::int as recipe_count
            from recipe_ingredients i
            join ingredient_groups g on g.id = i.group_id
            join recipes r on r.id = g.recipe_id
            where r.household_id = @householdId
            group by i.name
            order by recipe_count desc, i.name;
            """,
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => new IngredientUse(row.Name, row.RecipeCount))];
    }

    public async Task<IReadOnlyList<IngredientMerge>> MergesAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync<MergeRow>(
            "select from_name, to_name from ingredient_merges where household_id = @householdId order by from_name;",
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        // Every stored row was a valid merge when it was written.
        return [.. rows.Select(row => IngredientMerge.Create(row.FromName, row.ToName).Match(
            merge => merge,
            error => throw new InvalidOperationException($"Stored ingredient merge is invalid: {error.Code}.")))];
    }

    public async Task<int> MergeAsync(
        Guid householdId,
        IngredientMerge merge,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(merge);

        await RecordAsync(householdId, merge, now, cancellationToken).ConfigureAwait(false);

        var rewritten = await RewriteRecipesAsync(householdId, merge, now, cancellationToken).ConfigureAwait(false);

        if (merge.From.ComparisonKey != merge.To.ComparisonKey)
        {
            await MoveChoicesAsync(householdId, merge, cancellationToken).ConfigureAwait(false);
        }

        return rewritten;
    }

    public async Task RemoveMergeAsync(Guid householdId, string fromKey, CancellationToken cancellationToken) =>
        await executor.ExecuteAsync(
            "delete from ingredient_merges where household_id = @householdId and from_key = @fromKey;",
            new { householdId, fromKey },
            cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlySet<string>> KeptApartAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var keys = await executor.QueryAsync<string>(
            "select name_key from ingredient_merge_dismissals where household_id = @householdId;",
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        return keys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task KeepApartAsync(
        Guid householdId,
        string nameKey,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await executor.ExecuteAsync(
            """
            insert into ingredient_merge_dismissals (household_id, name_key, dismissed_at)
            values (@householdId, @nameKey, @now)
            on conflict (household_id, name_key) do nothing;
            """,
            new { householdId, nameKey, now },
            cancellationToken).ConfigureAwait(false);

    // The rule itself; rules that kept the merged name now keep the new one, and one that would
    // rename a name to exactly itself is dropped.
    private async Task RecordAsync(
        Guid householdId,
        IngredientMerge merge,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await executor.ExecuteAsync(
            """
            update ingredient_merges
            set to_key = @toKey, to_name = @toName
            where household_id = @householdId and to_key = @fromKey;

            delete from ingredient_merges
            where household_id = @householdId and from_name = to_name;

            insert into ingredient_merges (household_id, from_key, from_name, to_key, to_name, created_at)
            values (@householdId, @fromKey, @fromName, @toKey, @toName, @now)
            on conflict (household_id, from_key)
            do update set from_name = excluded.from_name, to_key = excluded.to_key,
                          to_name = excluded.to_name, created_at = excluded.created_at;
            """,
            new
            {
                householdId,
                fromKey = merge.From.ComparisonKey,
                fromName = merge.From.Value,
                toKey = merge.To.ComparisonKey,
                toName = merge.To.Value,
                now
            },
            cancellationToken).ConfigureAwait(false);

    // The fold is C#, so the spellings to replace are picked here and the update matches them exactly.
    private async Task<int> RewriteRecipesAsync(
        Guid householdId,
        IngredientMerge merge,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var uses = await UsesAsync(householdId, cancellationToken).ConfigureAwait(false);
        var spellings = uses
            .Select(use => use.Name)
            .Where(name => name != merge.To.Value && ItemName.Fold(name) == merge.From.ComparisonKey)
            .ToArray();

        if (spellings.Length == 0)
        {
            return 0;
        }

        // A rewrite is a change to each recipe, so its version moves and every cached copy is stale.
        var recipeIds = await executor.QueryAsync<Guid>(
            """
            with renamed as (
                update recipe_ingredients i
                set name = @to
                from ingredient_groups g
                join recipes r on r.id = g.recipe_id
                where g.id = i.group_id and r.household_id = @householdId and i.name = any(@spellings)
                returning g.recipe_id
            )
            update recipes
            set version = version + 1, updated_at = @now
            where id in (select recipe_id from renamed)
            returning id;
            """,
            new { householdId, to = merge.To.Value, spellings, now },
            cancellationToken).ConfigureAwait(false);

        foreach (var recipeId in recipeIds)
        {
            await documents.WriteAsync(recipeId, cancellationToken).ConfigureAwait(false);
        }

        return recipeIds.Count;
    }

    // What the household already said about the kept name wins; otherwise the merged name's choice moves over.
    private async Task MoveChoicesAsync(Guid householdId, IngredientMerge merge, CancellationToken cancellationToken)
    {
        foreach (var (table, sameRow) in ChoicesByName)
        {
            await executor.ExecuteAsync(
                $"""
                update {table} moved
                set name_key = @toKey
                where moved.household_id = @householdId and moved.name_key = @fromKey
                  and not exists (
                      select 1 from {table} kept
                      where kept.household_id = moved.household_id and kept.name_key = @toKey {sameRow}
                  );

                delete from {table} where household_id = @householdId and name_key = @fromKey;
                """,
                new { householdId, fromKey = merge.From.ComparisonKey, toKey = merge.To.ComparisonKey },
                cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record UseRow
    {
        public string Name { get; init; } = string.Empty;

        public int RecipeCount { get; init; }
    }

    private sealed record MergeRow
    {
        public string FromName { get; init; } = string.Empty;

        public string ToName { get; init; } = string.Empty;
    }
}
