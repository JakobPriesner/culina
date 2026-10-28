using Application.Abstractions;
using Domain.Nutrition;

namespace Infrastructure.Persistence.Nutrition;

internal sealed class HouseholdFoodRepository(DbExecutor executor) : IHouseholdFoodRepository
{
    private const string Columns =
        "id, household_id, name, energy_kj, energy_kcal, fat, saturated_fat, carbohydrate, sugars, protein, salt, gtin, source";

    public async Task<IReadOnlyList<HouseholdFood>> ForLibraryAsync(
        IReadOnlyList<Guid> library,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);

        var rows = await executor.QueryAsync<FoodRow>(
            $"""
            select {Columns}
            from household_foods
            where household_id = any(@library)
            order by name, created_at;
            """,
            new { library = library.ToArray() },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => row.ToFood())];
    }

    public async Task<HouseholdFood?> FindByGtinAsync(
        IReadOnlyList<Guid> library,
        string gtin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);

        var row = await executor.QuerySingleOrDefaultAsync<FoodRow>(
            $"""
            select {Columns}
            from household_foods
            where household_id = any(@library) and gtin = @gtin
            order by array_position(@library, household_id), created_at desc
            limit 1;
            """,
            new { library = library.ToArray(), gtin },
            cancellationToken).ConfigureAwait(false);

        return row?.ToFood();
    }

    public Task AddAsync(HouseholdFood food, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(food);

        var values = food.Food.Per100Grams;

        return executor.ExecuteAsync(
            """
            insert into household_foods
                (id, household_id, name, energy_kj, energy_kcal, fat, saturated_fat, carbohydrate, sugars, protein, salt,
                 gtin, source, created_at)
            values
                (@id, @householdId, @name, @energyKj, @energyKcal, @fat, @saturatedFat, @carbohydrate, @sugars, @protein, @salt,
                 @gtin, @source, @now);
            """,
            new
            {
                food.Id,
                food.HouseholdId,
                name = food.Food.NameDe,
                values.EnergyKj,
                values.EnergyKcal,
                values.Fat,
                values.SaturatedFat,
                values.Carbohydrate,
                values.Sugars,
                values.Protein,
                values.Salt,
                food.Gtin,
                food.Source,
                now
            },
            cancellationToken);
    }

    // Its choices go with it, the heirs' too: a choice of a food nobody has would count as an unknown one.
    public Task RemoveAsync(Guid householdId, Guid id, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            """
            delete from nutrition_food_overrides
            where food_code = @code
              and exists (select 1 from household_foods where id = @id and household_id = @householdId);

            delete from household_foods where id = @id and household_id = @householdId;
            """,
            new { householdId, id, code = HouseholdFoods.CodeOf(id) },
            cancellationToken);

    private sealed record FoodRow
    {
        public Guid Id { get; init; }

        public Guid HouseholdId { get; init; }

        public string Name { get; init; } = string.Empty;

        public decimal EnergyKj { get; init; }

        public decimal EnergyKcal { get; init; }

        public decimal? Fat { get; init; }

        public decimal? SaturatedFat { get; init; }

        public decimal? Carbohydrate { get; init; }

        public decimal? Sugars { get; init; }

        public decimal? Protein { get; init; }

        public decimal? Salt { get; init; }

        public string? Gtin { get; init; }

        public string Source { get; init; } = HouseholdFoodSources.Package;

        public HouseholdFood ToFood() =>
            new(
                Id,
                new Food(
                    HouseholdFoods.CodeOf(Id),
                    Name,
                    Name,
                    new Nutrients(EnergyKj, EnergyKcal, Fat, SaturatedFat, Carbohydrate, Sugars, Protein, Salt)),
                HouseholdId,
                Gtin,
                Source);
    }
}
