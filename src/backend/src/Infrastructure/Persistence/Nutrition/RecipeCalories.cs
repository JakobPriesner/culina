using Application.Abstractions;
using Domain.Nutrition;
using Domain.Recipes;
using Infrastructure.Persistence.Recipes;

namespace Infrastructure.Persistence.Nutrition;

/// <summary>Batch projection of the detail calculator; no per-card queries or second set of nutrition rules.</summary>
internal sealed class RecipeCalories(
    DbExecutor executor,
    IFoodTable foods,
    INutritionCorrectionRepository corrections,
    INutritionWeightRepository weights) : IRecipeCalories
{
    // Recovery and facets may ask several times within a request. Household choices are read once.
    private readonly Dictionary<string, IReadOnlyDictionary<Guid, LabelValue>> libraries = [];

    public async Task<IReadOnlyDictionary<Guid, LabelValue>> ReadAsync(
        Guid householdId,
        IReadOnlyList<Guid> library,
        IReadOnlyCollection<Guid>? recipeIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);
        if (recipeIds is { Count: 0 })
        {
            return new Dictionary<Guid, LabelValue>();
        }

        var key = $"{householdId}:{string.Join(',', library)}";
        if (libraries.TryGetValue(key, out var cached))
        {
            return recipeIds is null ? cached : cached.Where(pair => recipeIds.Contains(pair.Key)).ToDictionary();
        }

        var inputs = await executor.QueryMultipleAsync(
            """
            select id, yield_amount, yield_kind from recipes
            where household_id = any(@library) and (@all or id = any(@ids));

            select i.*, g.recipe_id from recipe_ingredients i
            join ingredient_groups g on g.id = i.group_id
            join recipes r on r.id = g.recipe_id
            where r.household_id = any(@library) and (@all or r.id = any(@ids));
            """,
            new { library = library.ToArray(), all = recipeIds is null, ids = recipeIds?.ToArray() ?? [] },
            async reader => (
                Recipes: (await reader.ReadAsync<YieldRow>().ConfigureAwait(false)).ToList(),
                Ingredients: (await reader.ReadAsync<IngredientRow>().ConfigureAwait(false)).ToList()),
            cancellationToken).ConfigureAwait(false);

        var choices = await corrections.AllAsync(householdId, cancellationToken).ConfigureAwait(false);
        var weighed = await weights.AllAsync(householdId, cancellationToken).ConfigureAwait(false);
        var typical = await weights.UsesTypicalWeightsAsync(householdId, cancellationToken).ConfigureAwait(false);
        var lines = inputs.Ingredients.ToLookup(line => line.RecipeId);
        var figures = new Dictionary<Guid, LabelValue>();

        foreach (var recipe in inputs.Recipes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var yield = Yield.Create(recipe.YieldAmount, RecipeCodes.ToYieldKind(recipe.YieldKind))
                .Match(value => value, error => throw new InvalidOperationException(error.Code));
            var figure = NutritionCalculator.Calculate(
                lines[recipe.Id].Select(line => RecipeAssembler.ToIngredient(new RecipeIngredientRow
                {
                    Id = line.Id,
                    Quantity = line.Quantity,
                    Unit = line.Unit,
                    Name = line.Name,
                    Note = line.Note
                })), yield, foods.Find, choices, weighed, typical);
            if (figure.Counted > 0)
            {
                figures[recipe.Id] = figure.Values.EnergyKcal;
            }
        }

        if (recipeIds is null)
        {
            libraries[key] = figures;
        }
        return figures;
    }

    private sealed record YieldRow
    {
        public Guid Id { get; init; }
        public decimal YieldAmount { get; init; }
        public string YieldKind { get; init; } = "servings";
    }

    private sealed record IngredientRow
    {
        public Guid RecipeId { get; init; }
        public Guid Id { get; init; }
        public decimal? Quantity { get; init; }
        public string? Unit { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Note { get; init; }
    }
}
