namespace Contracts.Nutrition;

/// <summary>What a household means by an ingredient name.</summary>
public sealed record SetFoodRequest
{
    /// <summary>A BLS code such as <c>Q611000</c>, or null for "do not count this".</summary>
    public string? Food { get; init; }
}

/// <summary>A food of the Bundeslebensmittelschlüssel, as a search result.</summary>
/// <param name="Code">The BLS code.</param>
/// <param name="NameDe">The German name.</param>
/// <param name="NameEn">The English name.</param>
/// <param name="EnergyKcal">Kilocalories per 100 g; null when the table has none.</param>
public sealed record FoodSummary(string Code, string NameDe, string NameEn, decimal? EnergyKcal);

/// <summary>A food a household typed from a package's label, per 100 g; null is unknown.</summary>
public sealed record HouseholdFoodDetail
{
    /// <summary>The food's id, which names it in the path.</summary>
    public required Guid Id { get; init; }

    /// <summary>The code to choose it by, as for a BLS food: <c>h:</c> and the id.</summary>
    public required string Code { get; init; }

    /// <summary>What the household calls it.</summary>
    public required string Name { get; init; }

    /// <summary>Energy in kilojoules.</summary>
    public required decimal EnergyKj { get; init; }

    /// <summary>Energy in kilocalories.</summary>
    public required decimal EnergyKcal { get; init; }

    /// <summary>Fat in grams.</summary>
    public decimal? Fat { get; init; }

    /// <summary>Saturated fat in grams.</summary>
    public decimal? SaturatedFat { get; init; }

    /// <summary>Carbohydrate in grams.</summary>
    public decimal? Carbohydrate { get; init; }

    /// <summary>Sugars in grams.</summary>
    public decimal? Sugars { get; init; }

    /// <summary>Protein in grams.</summary>
    public decimal? Protein { get; init; }

    /// <summary>Salt in grams.</summary>
    public decimal? Salt { get; init; }

    /// <summary>Whether the household asking typed it; otherwise one it inherits from did, and only that one removes it.</summary>
    public required bool Own { get; init; }
}

/// <summary>The foods a household can choose from beyond the table: its own and those of the households it inherits from.</summary>
public sealed record HouseholdFoodsResponse
{
    /// <summary>The foods, by name.</summary>
    public required IReadOnlyList<HouseholdFoodDetail> Items { get; init; }
}

/// <summary>A food from a package's label, per 100 g.</summary>
public sealed record CreateHouseholdFoodRequest
{
    /// <summary>What to call it: "Kokosmilch light".</summary>
    public required string Name { get; init; }

    /// <summary>Energy in kilojoules; worked out from the kilocalories when left out.</summary>
    public decimal? EnergyKj { get; init; }

    /// <summary>Energy in kilocalories.</summary>
    public required decimal EnergyKcal { get; init; }

    /// <summary>Fat in grams; null when the label does not say.</summary>
    public decimal? Fat { get; init; }

    /// <summary>Saturated fat in grams.</summary>
    public decimal? SaturatedFat { get; init; }

    /// <summary>Carbohydrate in grams.</summary>
    public decimal? Carbohydrate { get; init; }

    /// <summary>Sugars in grams.</summary>
    public decimal? Sugars { get; init; }

    /// <summary>Protein in grams.</summary>
    public decimal? Protein { get; init; }

    /// <summary>Salt in grams.</summary>
    public decimal? Salt { get; init; }
}

/// <summary>Foods that fit what was typed.</summary>
public sealed record FoodsResponse
{
    /// <summary>The foods, best first.</summary>
    public required IReadOnlyList<FoodSummary> Items { get; init; }
}
