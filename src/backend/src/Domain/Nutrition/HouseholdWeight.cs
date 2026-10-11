using Domain.Recipes;
using Domain.Shared;

namespace Domain.Nutrition;

/// <summary>What a household says one unit of an ingredient weighs: "bei uns wiegt 1 Zwiebel 150 g".</summary>
/// <param name="UnitKey">The canonical unit key, see <see cref="UnitKeys"/>.</param>
/// <param name="Grams">Grams of one unit.</param>
public sealed record HouseholdWeight(string UnitKey, decimal Grams)
{
    /// <summary>Reads a weight, or says why it is not one.</summary>
    /// <param name="unit">The unit as written: a spelling of a known key, or the household's own word.</param>
    /// <param name="grams">Grams of one unit.</param>
    public static Result<HouseholdWeight> Create(string? unit, decimal grams)
    {
        if (grams is <= 0m or > NutritionGrams.MostGramsPerUnit)
        {
            return NutritionErrors.InvalidGrams;
        }

        return Unit.Create(unit).Bind(one =>
        {
            if (NutritionGrams.HasOwnSize(one))
            {
                return NutritionErrors.UnitHasASize;
            }

            return Result<HouseholdWeight>.Success(new HouseholdWeight(UnitKeys.Of(one), grams));
        });
    }
}
