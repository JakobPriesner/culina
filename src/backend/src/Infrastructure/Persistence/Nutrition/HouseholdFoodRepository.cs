using Application.Abstractions;
using Domain.Nutrition;

namespace Infrastructure.Persistence.Nutrition;

internal sealed class HouseholdFoodRepository(DbExecutor executor) : IHouseholdFoodRepository
{
    public async Task<IReadOnlyList<HouseholdFood>> ForLibraryAsync(
        IReadOnlyList<Guid> library,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);

        var rows = await executor.QueryAsync<FoodRow>(
            """
            select id, household_id, name, energy_kj, energy_kcal, fat, saturated_fat, carbohydrate, sugars, protein, salt
            from household_foods
            where household_id = any(@library)
            order by name, created_at;
            """,
            new { library = library.ToArray() },
            cancellationToken).ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new HouseholdFood(
                row.Id,
                new Food(
                    HouseholdFoods.CodeOf(row.Id),
                    row.Name,
                    row.Name,
                    new Nutrients(
                        row.EnergyKj,
                        row.EnergyKcal,
                        row.Fat,
                        row.SaturatedFat,
                        row.Carbohydrate,
                        row.Sugars,
                        row.Protein,
                        row.Salt)),
                row.HouseholdId))
        ];
    }

    public Task AddAsync(Guid householdId, Guid id, Food food, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(food);

        var values = food.Per100Grams;

        return executor.ExecuteAsync(
            """
            insert into household_foods
                (id, household_id, name, energy_kj, energy_kcal, fat, saturated_fat, carbohydrate, sugars, protein, salt, created_at)
            values
                (@id, @householdId, @name, @energyKj, @energyKcal, @fat, @saturatedFat, @carbohydrate, @sugars, @protein, @salt, @now);
            """,
            new
            {
                id,
                householdId,
                name = food.NameDe,
                values.EnergyKj,
                values.EnergyKcal,
                values.Fat,
                values.SaturatedFat,
                values.Carbohydrate,
                values.Sugars,
                values.Protein,
                values.Salt,
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
    }
}
