using Application.Abstractions;

namespace Infrastructure.Persistence.Nutrition;

internal sealed class NutritionCorrectionRepository(DbExecutor executor, TimeProvider time)
    : INutritionCorrectionRepository
{
    public async Task<IReadOnlyDictionary<string, string?>> ForNamesAsync(
        Guid householdId,
        IReadOnlyCollection<string> nameKeys,
        CancellationToken cancellationToken)
    {
        if (nameKeys.Count == 0)
        {
            return new Dictionary<string, string?>();
        }

        var rows = await executor.QueryAsync<CorrectionRow>(
            """
            select name_key, food_code
            from nutrition_food_overrides
            where household_id = @householdId and name_key = any(@nameKeys);
            """,
            new { householdId, nameKeys = nameKeys.ToArray() },
            cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(row => row.NameKey, row => row.FoodCode, StringComparer.Ordinal);
    }

    public async Task SetAsync(
        Guid householdId,
        string nameKey,
        string? foodCode,
        CancellationToken cancellationToken) =>
        await executor.ExecuteAsync(
            """
            insert into nutrition_food_overrides (household_id, name_key, food_code, updated_at)
            values (@householdId, @nameKey, @foodCode, @now)
            on conflict (household_id, name_key)
            do update set food_code = excluded.food_code, updated_at = excluded.updated_at;
            """,
            new { householdId, nameKey, foodCode, now = time.GetUtcNow() },
            cancellationToken).ConfigureAwait(false);

    public async Task RemoveAsync(Guid householdId, string nameKey, CancellationToken cancellationToken) =>
        await executor.ExecuteAsync(
            "delete from nutrition_food_overrides where household_id = @householdId and name_key = @nameKey;",
            new { householdId, nameKey },
            cancellationToken).ConfigureAwait(false);

    private sealed record CorrectionRow
    {
        public string NameKey { get; init; } = string.Empty;

        public string? FoodCode { get; init; }
    }
}
