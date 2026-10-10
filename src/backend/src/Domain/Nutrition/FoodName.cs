namespace Domain.Nutrition;

/// <summary>One entry of <see cref="FoodNames"/>: the words for one BLS food.</summary>
/// <param name="Code">The BLS code of the food every form means.</param>
/// <param name="De">The German forms.</param>
/// <param name="En">The English forms.</param>
/// <param name="Density">Grams per millilitre, only for a food that pours; null otherwise.</param>
/// <param name="Egg">Which part of an egg this is, or <see cref="EggPart.None"/>.</param>
public sealed record FoodName(
    string Code,
    IReadOnlyList<string> De,
    IReadOnlyList<string> En,
    decimal? Density,
    EggPart Egg);
