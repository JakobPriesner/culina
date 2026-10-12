using Application.Abstractions;

namespace Infrastructure.Persistence.Nutrition;

internal sealed class NutritionCorrectionRepository(DbExecutor executor, TimeProvider time)
    : INutritionCorrectionRepository
{
    public async Task<IReadOnlyDictionary<string, FoodChoice>> ForNamesAsync(
        IReadOnlyList<Guid> library,
        IReadOnlyCollection<string>? nameKeys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (nameKeys is { Count: 0 })
        {
            return new Dictionary<string, FoodChoice>();
        }

        // The nearest household that said anything about a name decides it: an heir's own choice, else
        // the one it inherits.
        var rows = await executor.QueryAsync<LibraryCorrectionRow>(
            """
            select distinct on (name_key) name_key, food_code, household_id
            from nutrition_food_overrides
            where household_id = any(@library) and (@all or name_key = any(@nameKeys))
            order by name_key, array_position(@library, household_id);
            """,
            new { library = library.ToArray(), all = nameKeys is null, nameKeys = nameKeys?.ToArray() ?? [] },
            cancellationToken).ConfigureAwait(false);

        return rows.ToDictionary(
            row => row.NameKey,
            row => new FoodChoice(row.FoodCode, row.HouseholdId),
            StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<string, string?>> AllAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync<CorrectionRow>(
            "select name_key, food_code from nutrition_food_overrides where household_id = @householdId;",
            new { householdId },
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

    private sealed record LibraryCorrectionRow
    {
        public string NameKey { get; init; } = string.Empty;

        public string? FoodCode { get; init; }

        public Guid HouseholdId { get; init; }
    }

    private sealed record CorrectionRow
    {
        public string NameKey { get; init; } = string.Empty;

        public string? FoodCode { get; init; }
    }
}
