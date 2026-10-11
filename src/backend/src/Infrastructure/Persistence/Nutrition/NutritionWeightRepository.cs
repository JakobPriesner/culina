using Application.Abstractions;

namespace Infrastructure.Persistence.Nutrition;

internal sealed class NutritionWeightRepository(DbExecutor executor, TimeProvider time) : INutritionWeightRepository
{
    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>> ForNamesAsync(
        Guid householdId,
        IReadOnlyCollection<string> nameKeys,
        CancellationToken cancellationToken)
    {
        if (nameKeys.Count == 0)
        {
            return new Dictionary<string, IReadOnlyDictionary<string, decimal>>();
        }

        var rows = await executor.QueryAsync<WeightRow>(
            """
            select name_key, unit_key, grams
            from nutrition_unit_weights
            where household_id = @householdId and name_key = any(@nameKeys);
            """,
            new { householdId, nameKeys = nameKeys.ToArray() },
            cancellationToken).ConfigureAwait(false);

        return Grouped(rows);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>> AllAsync(
        Guid householdId,
        CancellationToken cancellationToken) =>
        Grouped(await executor.QueryAsync<WeightRow>(
            "select name_key, unit_key, grams from nutrition_unit_weights where household_id = @householdId;",
            new { householdId },
            cancellationToken).ConfigureAwait(false));

    public async Task SetAsync(
        Guid householdId,
        string nameKey,
        string unitKey,
        decimal grams,
        CancellationToken cancellationToken) =>
        await executor.ExecuteAsync(
            """
            insert into nutrition_unit_weights (household_id, name_key, unit_key, grams, updated_at)
            values (@householdId, @nameKey, @unitKey, @grams, @now)
            on conflict (household_id, name_key, unit_key)
            do update set grams = excluded.grams, updated_at = excluded.updated_at;
            """,
            new { householdId, nameKey, unitKey, grams, now = time.GetUtcNow() },
            cancellationToken).ConfigureAwait(false);

    public async Task RemoveAsync(
        Guid householdId,
        string nameKey,
        string unitKey,
        CancellationToken cancellationToken) =>
        await executor.ExecuteAsync(
            """
            delete from nutrition_unit_weights
            where household_id = @householdId and name_key = @nameKey and unit_key = @unitKey;
            """,
            new { householdId, nameKey, unitKey },
            cancellationToken).ConfigureAwait(false);

    public async Task<bool> UsesTypicalWeightsAsync(Guid householdId, CancellationToken cancellationToken) =>
        await executor.ExecuteScalarAsync<bool?>(
            "select use_typical_weights from nutrition_household_settings where household_id = @householdId;",
            new { householdId },
            cancellationToken).ConfigureAwait(false) ?? true;

    public async Task SetUsesTypicalWeightsAsync(Guid householdId, bool use, CancellationToken cancellationToken) =>
        await executor.ExecuteAsync(
            """
            insert into nutrition_household_settings (household_id, use_typical_weights, updated_at)
            values (@householdId, @use, @now)
            on conflict (household_id)
            do update set use_typical_weights = excluded.use_typical_weights, updated_at = excluded.updated_at;
            """,
            new { householdId, use, now = time.GetUtcNow() },
            cancellationToken).ConfigureAwait(false);

    private static Dictionary<string, IReadOnlyDictionary<string, decimal>> Grouped(IReadOnlyList<WeightRow> rows) =>
        rows.GroupBy(row => row.NameKey).ToDictionary(
            group => group.Key,
            group => (IReadOnlyDictionary<string, decimal>)group.ToDictionary(row => row.UnitKey, row => row.Grams),
            StringComparer.Ordinal);

    private sealed record WeightRow
    {
        public string NameKey { get; init; } = string.Empty;

        public string UnitKey { get; init; } = string.Empty;

        public decimal Grams { get; init; }
    }
}
