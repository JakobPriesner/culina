namespace Domain.Nutrition;

/// <summary>A food of the Bundeslebensmittelschlüssel, or one a household typed from a package, as it is bought and weighed.</summary>
/// <param name="Code">The BLS code, such as <c>Q611000</c>, or a household food's (<see cref="HouseholdFoods"/>).</param>
/// <param name="NameDe">The German name.</param>
/// <param name="NameEn">The English name.</param>
/// <param name="Per100Grams">What 100 g of the edible portion holds.</param>
public sealed record Food(string Code, string NameDe, string NameEn, Nutrients Per100Grams);
