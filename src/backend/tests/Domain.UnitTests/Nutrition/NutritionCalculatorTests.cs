using Domain.Nutrition;
using Domain.Recipes;
using Domain.Shopping;

namespace Domain.UnitTests.Nutrition;

public class NutritionCalculatorTests
{
    private static readonly Food Flour = Whole("C214100", kcal: 360m);
    private static readonly Food Oil = Whole("Q120000", kcal: 900m);
    private static readonly Food Butter = Whole("Q611000", kcal: 750m) with
    {
        Per100Grams = Whole("Q611000", kcal: 750m).Per100Grams with { SaturatedFat = null }
    };

    private static readonly Dictionary<string, Food> Table =
        new[] { Flour, Oil, Butter, Whole("Z999999", kcal: 100m), Whole("G480100", kcal: 28m), Whole("R111000", kcal: 0m), Whole("N110000", kcal: 0m), Whole("M111300", kcal: 64m) }.ToDictionary(food => food.Code);

    private static readonly IReadOnlyDictionary<string, string?> NoCorrections = new Dictionary<string, string?>();

    [Fact]
    public void Calculate_ShouldHaveNoLowerBound_WhenEveryLineIsCountedAndEveryFoodHasEveryValue()
    {
        // Act
        var result = Calculate(Servings(4m), NoCorrections, Line(200m, "g", "Mehl"), Line(100m, "ml", "Olivenöl"));

        // Assert
        Assert.True(result.Complete);
        Assert.Equal(2, result.Counted);
        Assert.All(AllValues(result), value => Assert.False(value.AtLeast));
        Assert.Equal((200m * 3.60m + 100m * 0.913m * 9.00m) / 4m, result.Values.EnergyKcal.Value);
    }

    [Fact]
    public void Calculate_ShouldMakeEveryValueALowerBound_WhenOneLineIsNotCounted()
    {
        // Act
        var result = Calculate(Servings(4m), NoCorrections, Line(200m, "g", "Mehl"), Line(1m, null, "Zwiebel"));

        // Assert
        Assert.False(result.Complete);
        Assert.Equal(1, result.Counted);
        Assert.Equal(2, result.Lines);
        Assert.All(AllValues(result), value => Assert.True(value.AtLeast));
        Assert.Equal(LineStatus.AmountNotInGrams, result.Ingredients[1].Status);
    }

    [Fact]
    public void Calculate_ShouldFlagOnlyTheValueAFoodLacks()
    {
        // Act
        var result = Calculate(Servings(1m), NoCorrections, Line(100m, "g", "Mehl"), Line(100m, "g", "Butter"));

        // Assert
        Assert.True(result.Complete);
        Assert.True(result.Values.SaturatedFat.AtLeast);
        Assert.Equal(0.1m, result.Values.SaturatedFat.Value);
        Assert.False(result.Values.EnergyKcal.AtLeast);
        Assert.False(result.Values.Fat.AtLeast);
    }

    [Fact]
    public void Calculate_ShouldReportUnknownAmountsAndNames()
    {
        // Act
        var result = Calculate(
            Servings(1m),
            NoCorrections,
            Line(null, null, "Mehl"),
            Line(5m, "g", "Xyzzy"),
            Line(1m, "g", "Mehl"));

        // Assert
        Assert.Equal(
            [LineStatus.NoAmount, LineStatus.UnknownFood, LineStatus.Counted],
            result.Ingredients.Select(line => line.Status));
    }

    [Fact]
    public void Calculate_ShouldDivideByThePieces_WhenTheRecipeMakesPieces()
    {
        // Act
        var result = Calculate(Yield.Create(12m, YieldKind.Pieces).Match(y => y, _ => throw new InvalidOperationException()), NoCorrections, Line(120m, "g", "Mehl"));

        // Assert
        Assert.Equal(YieldKind.Pieces, result.Per);
        Assert.Equal(12m, result.Yield);
        Assert.Equal(120m * 3.60m / 12m, result.Values.EnergyKcal.Value);
    }

    [Fact]
    public void Calculate_ShouldNotChangePerPortion_WhenAmountsAndYieldAreDoubled()
    {
        // Act
        var single = Calculate(Servings(4m), NoCorrections, Line(200m, "g", "Mehl"), Line(50m, "ml", "Olivenöl"));
        var doubled = Calculate(Servings(8m), NoCorrections, Line(400m, "g", "Mehl"), Line(100m, "ml", "Olivenöl"));

        // Assert
        Assert.Equal(single.Values, doubled.Values);
    }

    [Fact]
    public void Calculate_ShouldExcludeALine_WhenTheHouseholdSaidNotToCountIt()
    {
        // Arrange
        var corrections = new Dictionary<string, string?> { [ItemName.Fold("Mehl")] = null };

        // Act
        var result = Calculate(Servings(1m), corrections, Line(200m, "g", "Mehl"));

        // Assert
        Assert.Equal(LineStatus.Excluded, result.Ingredients[0].Status);
        Assert.True(result.Ingredients[0].Corrected);
        Assert.Equal(0, result.Counted);
    }

    [Fact]
    public void Calculate_ShouldCountTheCorrectedFood_ByMassOnlyWhenItHasNoNameEntry()
    {
        // Arrange
        var corrections = new Dictionary<string, string?> { [ItemName.Fold("Mehl")] = "Z999999" };

        // Act
        var result = Calculate(Servings(1m), corrections, Line(200m, "g", "Mehl"), Line(1m, "tbsp", "Mehl"));

        // Assert
        Assert.Equal("Z999999", result.Ingredients[0].Food!.Code);
        Assert.True(result.Ingredients[0].Corrected);
        Assert.Equal(GramsBasis.Mass, result.Ingredients[0].Via);
        Assert.Equal(LineStatus.AmountNotInGrams, result.Ingredients[1].Status);
    }

    [Fact]
    public void Calculate_ShouldUseTheDensityOfTheCorrectedFood_WhenItHasANameEntry()
    {
        // Arrange: Butter is corrected to the oil, which pours.
        var corrections = new Dictionary<string, string?> { [ItemName.Fold("Butter")] = Oil.Code };

        // Act
        var result = Calculate(Servings(1m), corrections, Line(10m, "ml", "Butter"));

        // Assert
        Assert.Equal(GramsBasis.Density, result.Ingredients[0].Via);
        Assert.Equal(9.13m, result.Ingredients[0].Grams);
    }

    [Fact]
    public void Calculate_ShouldTreatACorrectionToAFoodNotInTheTable_AsUnknown()
    {
        // Arrange
        var corrections = new Dictionary<string, string?> { [ItemName.Fold("Mehl")] = "Q000000" };

        // Act
        var result = Calculate(Servings(1m), corrections, Line(200m, "g", "Mehl"));

        // Assert
        Assert.Equal(LineStatus.UnknownFood, result.Ingredients[0].Status);
    }

    [Fact]
    public void Calculate_ShouldMakeTheLineEnergiesAddUpToTheTotal_WhenComplete()
    {
        // Act
        var result = Calculate(Servings(3m), NoCorrections, Line(200m, "g", "Mehl"), Line(1m, "tbsp", "Olivenöl"), Line(30m, "g", "Butter"));

        // Assert
        Assert.Equal(result.Values.EnergyKcal.Value, result.Ingredients.Sum(line => line.EnergyKcal!.Value));
    }

    [Fact]
    public void Calculate_ShouldReturnZerosAsLowerBounds_WhenNothingIsCounted()
    {
        // Act
        var result = Calculate(Servings(2m), NoCorrections, Line(null, null, "Mehl"));

        // Assert
        Assert.Equal(0, result.Counted);
        Assert.False(result.Complete);
        Assert.All(AllValues(result), value => Assert.Equal(new LabelValue(0m, AtLeast: true), value));
    }

    [Fact]
    public void Calculate_ShouldReturnZerosAsLowerBounds_WhenThereAreNoLines()
    {
        // Act
        var result = Calculate(Servings(2m), NoCorrections);

        // Assert
        Assert.False(result.Complete);
        Assert.All(AllValues(result), value => Assert.True(value.AtLeast));
    }

    [Fact]
    public void Calculate_ShouldKeepEnergyExact_WhenOnlyAFoodWithoutEnergyIsLeftOut()
    {
        // Act
        var result = Calculate(Servings(2m), NoCorrections, Line(100m, "g", "Mehl"), Line(null, null, "Salz"));

        // Assert
        Assert.False(result.Complete);
        Assert.Equal(1, result.Counted);
        Assert.False(result.Values.EnergyKcal.AtLeast);
        Assert.False(result.Values.EnergyKj.AtLeast);

        // The test salt has no energy but does have fat and salt, which a missing amount could still raise.
        Assert.True(result.Values.Fat.AtLeast);
        Assert.False(result.Ingredients[1].CanRaiseEnergy);
    }

    [Fact]
    public void Calculate_ShouldMakeEverythingExact_WhenWaterWithNoAmountIsLeftOut()
    {
        // Arrange
        var water = Table["N110000"] with { Per100Grams = new Nutrients(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m) };
        var table = new Dictionary<string, Food>(Table) { ["N110000"] = water };

        // Act
        var result = NutritionCalculator.Calculate(
            [Line(100m, "g", "Mehl"), Line(null, null, "Wasser")],
            Servings(1m),
            code => table.GetValueOrDefault(code),
            NoCorrections);

        // Assert
        Assert.All(AllValues(result), value => Assert.False(value.AtLeast));
        Assert.Equal(LineStatus.NoAmount, result.Ingredients[1].Status);
        Assert.False(result.Ingredients[1].CanRaiseEnergy);
    }

    [Fact]
    public void Calculate_ShouldStillFlagEnergy_WhenAFoodWithEnergyIsLeftOut()
    {
        // Act
        var result = Calculate(Servings(1m), NoCorrections, Line(100m, "g", "Olivenöl"), Line(1m, "tbsp", "Mehl"));

        // Assert
        Assert.True(result.Values.EnergyKcal.AtLeast);
        Assert.True(result.Ingredients[1].CanRaiseEnergy);
    }

    [Fact]
    public void Calculate_ShouldFlagEveryValue_WhenAFoodIsUnknown()
    {
        // Act
        var result = Calculate(Servings(1m), NoCorrections, Line(100m, "g", "Mehl"), Line(5m, "g", "Xyzzy"));

        // Assert
        Assert.All(AllValues(result), value => Assert.True(value.AtLeast));
        Assert.True(result.Ingredients[1].CanRaiseEnergy);
    }

    [Fact]
    public void Calculate_ShouldNotFlagAnything_WhenTheHouseholdExcludedTheOnlyLeftOutLine()
    {
        // Arrange
        var corrections = new Dictionary<string, string?> { [ItemName.Fold("Olivenöl")] = null };

        // Act
        var result = Calculate(Servings(1m), corrections, Line(100m, "g", "Mehl"), Line(1m, "tbsp", "Olivenöl"));

        // Assert
        Assert.All(AllValues(result), value => Assert.False(value.AtLeast));
        Assert.False(result.Ingredients[1].CanRaiseEnergy);
    }

    [Fact]
    public void Calculate_ShouldCarryTheReason_ForALineWhoseUnitIsNotCounted()
    {
        // Act
        var result = Calculate(
            Servings(1m),
            NoCorrections,
            Line(2m, "tbsp", "Mehl"),
            Line(1m, "ml", "Mehl"),
            Line(1m, null, "Zwiebel"),
            Line(1m, "clove", "Mehl"));

        // Assert
        Assert.Equal(
            [GramsRefusal.SpoonOfSolid, GramsRefusal.VolumeOfSolid, GramsRefusal.Count, GramsRefusal.HouseholdUnit],
            result.Ingredients.Select(line => line.Refusal));
    }

    [Fact]
    public void Calculate_ShouldNotCountAnImplausibleAmount_AndFlagEveryValue()
    {
        // Act: 1800 l of milk, meant as 1800 ml.
        var result = Calculate(Servings(4m), NoCorrections, Line(100m, "g", "Mehl"), Line(1800m, "l", "Milch"));

        // Assert
        var milk = result.Ingredients[1];
        Assert.Equal(LineStatus.Implausible, milk.Status);
        Assert.Equal(1800m * 1000m * 1.031m, milk.Grams);
        Assert.Null(milk.EnergyKcal);
        Assert.True(milk.CanRaiseEnergy);
        Assert.Equal(1, result.Counted);
        Assert.Equal(100m * 3.60m / 4m, result.Values.EnergyKcal.Value);
        Assert.All(AllValues(result), value => Assert.True(value.AtLeast));
    }

    [Fact]
    public void Calculate_ShouldCountABigBatch_ThatClaimsOnePortion()
    {
        // Act: a whole cake whose recipe says it makes one portion.
        var result = Calculate(Servings(1m), NoCorrections, Line(2.5m, "kg", "Mehl"));

        // Assert: the yield is wrong, not the amount, so the line still counts.
        Assert.Equal(LineStatus.Counted, result.Ingredients[0].Status);
    }

    [Fact]
    public void Calculate_ShouldCountLitreAndAHalfOfMilk_InAFourPortionRecipe()
    {
        // Act
        var result = Calculate(Servings(4m), NoCorrections, Line(1.8m, "l", "Milch"));

        // Assert
        Assert.Equal(LineStatus.Counted, result.Ingredients[0].Status);
        Assert.True(result.Complete);
    }

    private static IEnumerable<LabelValue> AllValues(NutritionResult result) =>
    [
        result.Values.EnergyKj,
        result.Values.EnergyKcal,
        result.Values.Fat,
        result.Values.SaturatedFat,
        result.Values.Carbohydrate,
        result.Values.Sugars,
        result.Values.Protein,
        result.Values.Salt
    ];

    private static NutritionResult Calculate(
        Yield yield,
        IReadOnlyDictionary<string, string?> corrections,
        params RecipeIngredient[] lines) =>
        NutritionCalculator.Calculate(lines, yield, code => Table.GetValueOrDefault(code), corrections);

    private static Yield Servings(decimal amount) =>
        Yield.Create(amount, YieldKind.Servings).Match(y => y, _ => throw new InvalidOperationException());

    private static Food Whole(string code, decimal kcal) =>
        new(code, "de", "en", new Nutrients(kcal * 4m, kcal, 1m, 0.1m, 70m, 1m, 10m, 0.01m));

    private static RecipeIngredient Line(decimal? amount, string? unit, string name)
    {
        var resolved = unit is null ? null : Unit.Create(unit).Match<Unit?>(one => one, _ => null);
        var quantity = Quantity.Create(amount, resolved).Match(one => one, _ => throw new InvalidOperationException());

        return RecipeIngredient.Create(null, 0, quantity, name, null).Match(one => one, _ => throw new InvalidOperationException());
    }
}
