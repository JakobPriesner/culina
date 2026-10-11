namespace Domain.Nutrition;

/// <summary>One entry of <see cref="FoodNames"/>: the words for one BLS food.</summary>
/// <param name="Code">The BLS code of the food every form means.</param>
/// <param name="De">The German forms.</param>
/// <param name="En">The English forms.</param>
/// <param name="Density">Grams per millilitre, only for a food that pours; null otherwise.</param>
/// <param name="Egg">Which part of an egg this is, or <see cref="EggPart.None"/>.</param>
/// <param name="LabelDe">What a reader calls the default food in German, in cooking language ("Hühnerei").</param>
/// <param name="LabelEn">What a reader calls the default food in English.</param>
/// <param name="Liquid">The same word as a liquid, when one word covers a powder and a liquid (broth); null otherwise.</param>
/// <param name="UnitForms">The forms that already name a unit ("Knoblauchzehe"), so a bare count of them is that unit; null otherwise.</param>
/// <param name="ImpliedUnit">
/// Set on the entry <see cref="FoodNames.Match"/> returns, when the name that matched is one of <paramref name="UnitForms"/>:
/// the canonical unit key a bare count or a piece of that name means. Null otherwise.
/// </param>
public sealed record FoodName(
    string Code,
    IReadOnlyList<string> De,
    IReadOnlyList<string> En,
    decimal? Density,
    EggPart Egg,
    string LabelDe,
    string LabelEn,
    LiquidFood? Liquid = null,
    UnitForms? UnitForms = null,
    string? ImpliedUnit = null)
{
    /// <summary>The liquid alternative as an entry of its own, which pours at its own density; this entry when it has none.</summary>
    public FoodName Resolved(bool asLiquid) =>
        asLiquid && Liquid is { } liquid
            ? new FoodName(liquid.Code, [], [], liquid.Density, EggPart.None, liquid.LabelDe, liquid.LabelEn)
            : this;
}

/// <summary>The liquid form of a food that is bought as a powder: its BLS food, density and names.</summary>
/// <param name="Code">The BLS code of the liquid.</param>
/// <param name="Density">Grams per millilitre.</param>
/// <param name="LabelDe">The German reader-facing name.</param>
/// <param name="LabelEn">The English reader-facing name.</param>
public sealed record LiquidFood(string Code, decimal Density, string LabelDe, string LabelEn);

/// <summary>Forms of a food that already name a unit: "Knoblauchzehe(n)", "garlic clove(s)".</summary>
/// <param name="UnitKey">The canonical unit key (see <see cref="UnitKeys"/>) a bare count of these forms means.</param>
/// <param name="Forms">The forms, written as in <see cref="FoodName.De"/> and <see cref="FoodName.En"/>.</param>
public sealed record UnitForms(string UnitKey, IReadOnlyList<string> Forms);
