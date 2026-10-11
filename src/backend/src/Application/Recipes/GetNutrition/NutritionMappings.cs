using Contracts.Recipes.GetNutrition;
using Domain.Nutrition;
using Domain.Recipes;

namespace Application.Recipes.GetNutrition;

internal static class NutritionMappings
{
    internal static Response ToResponse(this NutritionResult figure) =>
        new()
        {
            Per = figure.Per == YieldKind.Pieces ? "piece" : "serving",
            Yield = figure.Yield,
            Complete = figure.Complete,
            Counted = figure.Counted,
            Lines = figure.Lines,
            Values = figure.Values.ToContract(),
            Ingredients = [.. figure.Ingredients.Select(ToContract)],
            Source = new NutritionSource
            {
                Name = NutritionData.Source,
                Version = NutritionData.SourceVersion,
                Publisher = "Max Rubner-Institut",
                Licence = "CC BY 4.0"
            }
        };

    private static NutritionValues ToContract(this LabelValues values) =>
        new()
        {
            EnergyKj = values.EnergyKj.ToContract(),
            EnergyKcal = values.EnergyKcal.ToContract(),
            Fat = values.Fat.ToContract(),
            SaturatedFat = values.SaturatedFat.ToContract(),
            Carbohydrate = values.Carbohydrate.ToContract(),
            Sugars = values.Sugars.ToContract(),
            Protein = values.Protein.ToContract(),
            Salt = values.Salt.ToContract()
        };

    private static Contracts.Recipes.GetNutrition.NutritionValue ToContract(this LabelValue value) =>
        new() { Value = value.Value, AtLeast = value.AtLeast, Estimated = value.Estimated };

    private static NutritionIngredient ToContract(NutritionLine line) =>
        new()
        {
            IngredientId = line.IngredientId,
            Status = line.Status switch
            {
                LineStatus.Counted => "counted",
                LineStatus.AmountNotInGrams => "amountNotInGrams",
                LineStatus.NoAmount => "noAmount",
                LineStatus.UnknownFood => "unknownFood",
                LineStatus.Implausible => "implausible",
                _ => "excluded"
            },
            Reason = line.Refusal switch
            {
                GramsRefusal.SpoonOfSolid => "spoonOfSolid",
                GramsRefusal.VolumeOfSolid => "volumeOfSolid",
                GramsRefusal.Count => "count",
                GramsRefusal.HouseholdUnit => "householdUnit",
                _ => null
            },
            Food = line.Food?.ToContract(line.LabelDe, line.LabelEn),
            Grams = line.Grams,
            Via = line.Via switch
            {
                GramsBasis.Mass => "mass",
                GramsBasis.Density => "density",
                GramsBasis.EggSize => "eggSize",
                GramsBasis.HouseholdWeight => "householdWeight",
                GramsBasis.TypicalWeight => "typicalWeight",
                _ => null
            },
            Corrected = line.Corrected,
            CanRaiseEnergy = line.CanRaiseEnergy,
            EnergyKcal = line.EnergyKcal,
            UnitKey = line.UnitKey,
            Source = line.Source,
            Variants = line.Variants is { Count: > 0 } variants
                ? [.. variants.Select(variant => new NutritionVariant
                {
                    Code = variant.Code,
                    LabelDe = variant.LabelDe,
                    LabelEn = variant.LabelEn,
                    EnergyKcal = variant.EnergyKcal
                })]
                : null
        };

    /// <summary>A food as the nutrition answer names it; its labels are the BLS names unless the table has better.</summary>
    internal static NutritionFood ToContract(this Food food, string? labelDe, string? labelEn) =>
        new()
        {
            Code = food.Code,
            NameDe = food.NameDe,
            NameEn = food.NameEn,
            LabelDe = labelDe ?? food.NameDe,
            LabelEn = labelEn ?? food.NameEn
        };
}
