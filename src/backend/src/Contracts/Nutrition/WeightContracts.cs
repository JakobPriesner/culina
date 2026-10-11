using Contracts.Recipes.GetNutrition;

namespace Contracts.Nutrition;

/// <summary>What a household says one unit of an ingredient weighs.</summary>
public sealed record SetUnitWeightRequest
{
    /// <summary>Grams of one unit: more than 0, at most 10000.</summary>
    public decimal Grams { get; init; }
}

/// <summary>How a household's nutrition figures are worked out.</summary>
public sealed record NutritionSettings
{
    /// <summary>
    /// Whether a typical weight (<c>via: typicalWeight</c>) may count a line nothing else counts: an onion, a clove,
    /// a spoon of butter. True until a household says otherwise.
    /// </summary>
    public required bool UseTypicalWeights { get; init; }
}

/// <summary>What a household has said about its ingredients, for the nutrition figures.</summary>
public sealed record IngredientFactsResponse
{
    /// <summary>One entry per ingredient name with a food choice or a weight, by name.</summary>
    public required IReadOnlyList<IngredientFact> Items { get; init; }
}

/// <summary>What a household has said about one ingredient name.</summary>
public sealed record IngredientFact
{
    /// <summary>The name, in the folded form every spelling of it shares: lower case, <c>ü</c> written <c>ue</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the household chose the food, or chose to leave the ingredient out.</summary>
    public required bool Corrected { get; init; }

    /// <summary>The food the household chose; absent when it did not, or when it chose not to count the ingredient.</summary>
    public NutritionFood? Food { get; init; }

    /// <summary>The weights the household set, by unit.</summary>
    public required IReadOnlyList<UnitWeight> Weights { get; init; }
}

/// <summary>What one unit weighs, as a household says.</summary>
public sealed record UnitWeight
{
    /// <summary>The canonical unit key: <c>piece</c>, <c>clove</c>, <c>tbsp</c>, or a household's own word, folded.</summary>
    public required string Unit { get; init; }

    /// <summary>Grams of one unit.</summary>
    public required decimal Grams { get; init; }
}
