namespace Domain.Nutrition;

/// <summary>Where nutrition values come from, and the version that stands for all of it.</summary>
public static class NutritionData
{
    /// <summary>The table the values are read from.</summary>
    public const string Source = "Bundeslebensmittelschlüssel";

    /// <summary>The edition of <see cref="Source"/> shipped.</summary>
    public const string SourceVersion = "4.0";

    /// <summary>
    /// Raise when <c>bls.tsv</c> or the rules in <see cref="NutritionGrams"/> change; a change to
    /// <see cref="FoodNames"/> raises <see cref="Version"/> through its own version.
    /// </summary>
    private const int ExtractAndRulesVersion = 2;

    /// <summary>
    /// What an ETag over a nutrition figure includes, so a deploy with new data is never answered
    /// with a 304 from before it.
    /// </summary>
    public const int Version = ExtractAndRulesVersion + FoodNames.Version;
}
