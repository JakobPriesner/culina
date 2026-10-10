namespace Contracts.Recipes.GetNutrition;

/// <summary>A recipe's nutrition per portion or per piece, with the lines it covers.</summary>
public sealed record Response
{
    /// <summary>What the figures are per: <c>serving</c> or <c>piece</c>.</summary>
    public required string Per { get; init; }

    /// <summary>The amount of servings or pieces the recipe makes, which the totals were divided by.</summary>
    public required decimal Yield { get; init; }

    /// <summary>Whether every ingredient line is counted. A value can still be a lower bound when a food lacks it.</summary>
    public required bool Complete { get; init; }

    /// <summary>How many ingredient lines are counted.</summary>
    public required int Counted { get; init; }

    /// <summary>How many ingredient lines the recipe has.</summary>
    public required int Lines { get; init; }

    /// <summary>The label values, per portion or piece.</summary>
    public required NutritionValues Values { get; init; }

    /// <summary>Every ingredient line, in recipe order.</summary>
    public required IReadOnlyList<NutritionIngredient> Ingredients { get; init; }

    /// <summary>Where the numbers come from; shown wherever they are.</summary>
    public required NutritionSource Source { get; init; }
}

/// <summary>The label values of one portion or piece.</summary>
public sealed record NutritionValues
{
    /// <summary>Energy in kilojoules.</summary>
    public required NutritionValue EnergyKj { get; init; }

    /// <summary>Energy in kilocalories.</summary>
    public required NutritionValue EnergyKcal { get; init; }

    /// <summary>Fat in grams.</summary>
    public required NutritionValue Fat { get; init; }

    /// <summary>Saturated fat in grams.</summary>
    public required NutritionValue SaturatedFat { get; init; }

    /// <summary>Carbohydrate in grams.</summary>
    public required NutritionValue Carbohydrate { get; init; }

    /// <summary>Sugars in grams.</summary>
    public required NutritionValue Sugars { get; init; }

    /// <summary>Protein in grams.</summary>
    public required NutritionValue Protein { get; init; }

    /// <summary>Salt in grams.</summary>
    public required NutritionValue Salt { get; init; }
}

/// <summary>One label value.</summary>
public sealed record NutritionValue
{
    /// <summary>The sum over the counted lines, unrounded.</summary>
    public required decimal Value { get; init; }

    /// <summary>True when a line left out, or a food without this value, could only have added to it.</summary>
    public required bool AtLeast { get; init; }
}

/// <summary>What became of one ingredient line.</summary>
public sealed record NutritionIngredient
{
    /// <summary>The recipe's ingredient line.</summary>
    public required Guid IngredientId { get; init; }

    /// <summary><c>counted</c>, <c>amountNotInGrams</c>, <c>noAmount</c>, <c>unknownFood</c>, <c>excluded</c> or <c>implausible</c>.</summary>
    public required string Status { get; init; }

    /// <summary>
    /// Why the unit is not counted: <c>spoonOfSolid</c>, <c>volumeOfSolid</c>, <c>count</c> or <c>householdUnit</c>;
    /// only when the status is <c>amountNotInGrams</c>.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>The food the line was recognised as, when it was.</summary>
    public NutritionFood? Food { get; init; }

    /// <summary>The grams counted, unrounded; when the status is <c>implausible</c>, the grams it would have been.</summary>
    public decimal? Grams { get; init; }

    /// <summary>How the grams were reached: <c>mass</c>, <c>density</c> or <c>eggSize</c>; whenever there are grams.</summary>
    public string? Via { get; init; }

    /// <summary>Whether the household chose the food (or to leave it out) instead of the default.</summary>
    public required bool Corrected { get; init; }

    /// <summary>
    /// Whether this line, left out of the figure, could still add energy to it: it is not counted, was not excluded
    /// by the household, and its food is unknown or has energy (or no energy value). True for an implausible amount.
    /// </summary>
    public required bool CanRaiseEnergy { get; init; }

    /// <summary>This line's energy per portion, in kilocalories, unrounded; the lines add up to the energy value.</summary>
    public decimal? EnergyKcal { get; init; }
}

/// <summary>A food of the Bundeslebensmittelschlüssel.</summary>
public sealed record NutritionFood
{
    /// <summary>The BLS code.</summary>
    public required string Code { get; init; }

    /// <summary>The German BLS name, the citation.</summary>
    public required string NameDe { get; init; }

    /// <summary>The English BLS name, the citation.</summary>
    public required string NameEn { get; init; }

    /// <summary>What a reader calls the food in German; the BLS name for a food chosen by correction that has no label of its own.</summary>
    public required string LabelDe { get; init; }

    /// <summary>What a reader calls the food in English; the BLS name for a food chosen by correction that has no label of its own.</summary>
    public required string LabelEn { get; init; }
}

/// <summary>Where the data comes from, for attribution (CC BY 4.0 requires it wherever it is shown).</summary>
public sealed record NutritionSource
{
    /// <summary>The table's name.</summary>
    public required string Name { get; init; }

    /// <summary>The edition.</summary>
    public required string Version { get; init; }

    /// <summary>Who publishes it.</summary>
    public required string Publisher { get; init; }

    /// <summary>The licence it is used under.</summary>
    public required string Licence { get; init; }
}
