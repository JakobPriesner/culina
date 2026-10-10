namespace Domain.Nutrition;

/// <summary>
/// The values of a European food label, for some quantity of food; null is unknown, never zero.
/// </summary>
/// <param name="EnergyKj">Energy in kilojoules.</param>
/// <param name="EnergyKcal">Energy in kilocalories.</param>
/// <param name="Fat">Fat in grams.</param>
/// <param name="SaturatedFat">Saturated fat in grams.</param>
/// <param name="Carbohydrate">Carbohydrate in grams.</param>
/// <param name="Sugars">Sugars in grams.</param>
/// <param name="Protein">Protein in grams.</param>
/// <param name="Salt">Salt in grams.</param>
public sealed record Nutrients(
    decimal? EnergyKj,
    decimal? EnergyKcal,
    decimal? Fat,
    decimal? SaturatedFat,
    decimal? Carbohydrate,
    decimal? Sugars,
    decimal? Protein,
    decimal? Salt);
