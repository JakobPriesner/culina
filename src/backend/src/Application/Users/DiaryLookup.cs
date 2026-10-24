using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Recipes;
using Application.Recipes.GetNutrition;
using Contracts.Recipes.GetNutrition;
using Domain.Diary;
using Domain.Nutrition;
using Domain.Shared;

namespace Application.Users;

/// <summary>What a person says they ate, before its figures are looked up: one of a recipe, a food or typed figures.</summary>
/// <param name="RecipeId">A recipe.</param>
/// <param name="Portions">How many portions of it; one when left out.</param>
/// <param name="HouseholdId">The household whose recipe or own food it is.</param>
/// <param name="CookLogEntryId">The "made it" a portion comes from.</param>
/// <param name="FoodCode">A food: a BLS code or a household food's.</param>
/// <param name="Amount">How much of the food.</param>
/// <param name="Unit">In which unit; grams when left out.</param>
/// <param name="Label">Typed: what it was.</param>
/// <param name="Kcal">Typed: kilocalories.</param>
/// <param name="Protein">Typed: protein in grams.</param>
/// <param name="Carbohydrate">Typed: carbohydrate in grams.</param>
/// <param name="Fat">Typed: fat in grams.</param>
public sealed record DiaryWhat(
    Guid? RecipeId,
    decimal? Portions,
    Guid? HouseholdId,
    Guid? CookLogEntryId,
    string? FoodCode,
    decimal? Amount,
    string? Unit,
    string? Label,
    decimal? Kcal,
    decimal? Protein,
    decimal? Carbohydrate,
    decimal? Fat);

/// <summary>
/// Turns what a person ate into a diary entry with looked-up figures: a recipe through its own nutrition, a food
/// through the table (or the household's own) and a cited typical weight, typed figures as typed. Adding and changing
/// an entry both come through here, so a changed amount is looked up exactly as a new one.
/// </summary>
internal sealed class DiaryLookup(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IQueryHandler<GetNutritionQuery, RecipeNutrition> nutrition,
    IFoodTable foods,
    IHouseholdFoodRepository ownFoods,
    TimeProvider time)
{
    internal async Task<Result<DiaryEntry>> LogAsync(
        Guid userId,
        DateOnly date,
        DiaryMeal meal,
        DiaryWhat what,
        CancellationToken cancellationToken)
    {
        var typed = what.Label is not null || what.Kcal is not null;
        var named = (what.RecipeId is null ? 0 : 1) + (what.FoodCode is null ? 0 : 1) + (typed ? 1 : 0);

        if (named != 1)
        {
            return DiaryErrors.UnclearSource;
        }

        if (what.RecipeId is { } recipeId)
        {
            return await RecipeAsync(userId, date, meal, recipeId, what, cancellationToken).ConfigureAwait(false);
        }

        if (what.FoodCode is { } code)
        {
            return await FoodAsync(userId, date, meal, code.Trim(), what, cancellationToken).ConfigureAwait(false);
        }

        return DiaryEntry.Typed(
            userId,
            date,
            meal,
            what.Label,
            new DiaryFigures(what.Kcal, what.Protein, what.Carbohydrate, what.Fat, null, null, null),
            NutritionData.Version,
            time.GetUtcNow());
    }

    private async Task<Result<DiaryEntry>> RecipeAsync(
        Guid userId,
        DateOnly date,
        DiaryMeal meal,
        Guid recipeId,
        DiaryWhat what,
        CancellationToken cancellationToken)
    {
        var visible = await RecipeAccess
            .VisibleInAsync(recipes, households, recipeId, what.HouseholdId, userId, cancellationToken)
            .ConfigureAwait(false);

        return await visible.Match(
            async recipe =>
            {
                // The same figure the recipe page shows, per portion (or piece), with the household's corrections.
                var figure = await nutrition
                    .Handle(new GetNutritionQuery(recipeId, userId, what.HouseholdId), cancellationToken)
                    .ConfigureAwait(false);

                return figure.Bind(found => DiaryEntry.OfRecipe(
                    userId,
                    date,
                    meal,
                    new DiaryLink(recipe.Id, what.HouseholdId ?? recipe.HouseholdId, null, what.CookLogEntryId),
                    what.Portions ?? 1,
                    recipe.Title.Value,
                    DiaryFigures.OfUnits(PerPortion(found.Body.Values), what.Portions ?? 1),
                    TrustOf(found.Body.Values),
                    NutritionData.Version,
                    time.GetUtcNow()));
            },
            error => Task.FromResult(Result<DiaryEntry>.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Result<DiaryEntry>> FoodAsync(
        Guid userId,
        DateOnly date,
        DiaryMeal meal,
        string code,
        DiaryWhat what,
        CancellationToken cancellationToken)
    {
        var own = HouseholdFoods.IsOwn(code);

        // A household's own food is only found among the foods of a household the person is in.
        var available = !own
            ? Result<IReadOnlyList<HouseholdFood>>.Success([])
            : what.HouseholdId is not { } household
                ? DiaryErrors.FoodUnknown
                : await OwnFoodsAsync(household, userId, cancellationToken).ConfigureAwait(false);

        return available.Bind(ownFoodsOf =>
        {
            if (FoodLookup.With(foods, ownFoodsOf)(code) is not { } food)
            {
                return DiaryErrors.FoodUnknown;
            }

            var unit = string.IsNullOrWhiteSpace(what.Unit) ? DiaryAmount.Gram : what.Unit.Trim();
            var amount = what.Amount ?? (unit == DiaryAmount.Gram ? 100m : 1m);

            if (unit == DiaryAmount.Gram)
            {
                return Logged(food, amount, amount, DiaryTrust.Exact);
            }

            // Any other unit is an estimate from a cited typical weight; a household's own food has none.
            return (own ? null : TypicalWeights.Find(code, unit)) is { CountAs: null } typical
                ? Logged(food, amount, amount * typical.Grams, new DiaryTrust(false, true, typical.Source))
                : DiaryErrors.UnitUnknown;

            Result<DiaryEntry> Logged(Food eaten, decimal value, decimal grams, DiaryTrust trust) =>
                DiaryEntry.OfFood(
                    userId,
                    date,
                    meal,
                    new DiaryLink(null, own ? what.HouseholdId : null, code, what.CookLogEntryId),
                    new DiaryAmount(value, unit, grams),
                    eaten.NameDe,
                    DiaryFigures.Of(eaten.Per100Grams, grams),
                    trust,
                    NutritionData.Version,
                    time.GetUtcNow());
        });
    }

    private async Task<Result<IReadOnlyList<HouseholdFood>>> OwnFoodsAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var library = await HouseholdAccess
            .LibraryOfMemberAsync(households, householdId, userId, cancellationToken)
            .ConfigureAwait(false);

        return await library.Match(
            async found => Result<IReadOnlyList<HouseholdFood>>.Success(
                await ownFoods.ForLibraryAsync(found, cancellationToken).ConfigureAwait(false)),
            error => Task.FromResult(Result<IReadOnlyList<HouseholdFood>>.Failure(error))).ConfigureAwait(false);
    }

    // A value nothing counted towards is unknown, not zero: zero there would read as "ate nothing of it".
    private static decimal? Known(NutritionValue value) => value.AtLeast && value.Value == 0 ? null : value.Value;

    private static Nutrients PerPortion(NutritionValues values) =>
        new(
            null,
            Known(values.EnergyKcal),
            Known(values.Fat),
            Known(values.SaturatedFat),
            Known(values.Carbohydrate),
            Known(values.Sugars),
            Known(values.Protein),
            Known(values.Salt));

    // The diary shows energy and the three macros, so their flags decide how far the entry can be trusted.
    private static DiaryTrust TrustOf(NutritionValues values)
    {
        NutritionValue[] shown = [values.EnergyKcal, values.Protein, values.Carbohydrate, values.Fat];

        return new DiaryTrust(
            shown.Any(value => value.AtLeast),
            shown.Any(value => value.Estimated),
            null);
    }
}
