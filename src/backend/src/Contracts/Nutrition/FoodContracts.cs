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

/// <summary>Foods that fit what was typed.</summary>
public sealed record FoodsResponse
{
    /// <summary>The foods, best first.</summary>
    public required IReadOnlyList<FoodSummary> Items { get; init; }
}
