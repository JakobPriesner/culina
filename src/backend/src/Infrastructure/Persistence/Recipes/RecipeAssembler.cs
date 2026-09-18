using Domain.Recipes;
using Domain.Shared;

namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// Rebuilds a recipe aggregate from the six result sets that make it up.
/// </summary>
/// <remarks>
/// A row that no longer parses is a defect in the data, not an expected
/// outcome: these values were written by this application, so a failure here
/// means something corrupted them and a <c>Result</c> nobody could act on would
/// only hide it.
/// </remarks>
internal static class RecipeAssembler
{
    internal static Recipe Assemble(
        IReadOnlyList<IngredientGroupRow> groupRows,
        IReadOnlyList<RecipeIngredientRow> ingredientRows,
        IReadOnlyList<StepRow> stepRows,
        IReadOnlyList<StepIngredientRefRow> useRows,
        IReadOnlyList<string> tagSlugs,
        RecipeRow row)
    {
        var uses = useRows
            .GroupBy(reference => reference.StepId)
            .ToDictionary(
                step => step.Key,
                step => (IReadOnlyCollection<Guid>)[
                    .. step.Select(reference => reference.RecipeIngredientId)
                ]);

        var recipe = Recipe.Restore(
            row.Id,
            row.HouseholdId,
            Unwrap(RecipeTitle.Create(row.Title), "title"),
            row.CreatedBy,
            row.CreatedAt,
            row.UpdatedAt,
            row.Version);

        recipe.Describe(
            new RecipeDetails(
                recipe.Title,
                row.Description,
                RecipeCodes.ToLanguage(row.RecipeLanguage),
                Unwrap(
                    Yield.Create(
                        row.YieldAmount,
                        RecipeCodes.ToYieldKind(row.YieldKind),
                        row.YieldLabel),
                    "yield"),
                row.PrepMinutes,
                row.CookMinutes,
                tagSlugs),
            row.UpdatedAt);

        // Unlike the other two, this one can fail on values that were valid
        // when they were written — a step's needs are checked against the
        // ingredient list, and the two are read from separate tables.
        Expect(
            recipe.SetContents(
                [.. groupRows.Select(group => ToGroup(group, ingredientRows))],
                [.. stepRows.Select(step => ToStep(step, uses))],
                row.UpdatedAt),
            "contents");

        recipe.SetImage(row.ImageId, row.UpdatedAt);
        recipe.AcceptVersion(row.Version);

        return recipe;
    }

    private static IngredientGroup ToGroup(
        IngredientGroupRow group,
        IReadOnlyList<RecipeIngredientRow> ingredients) =>
        Unwrap(
            IngredientGroup.Create(
                group.Id,
                group.Name,
                group.SortOrder,
                [.. ingredients.Where(i => i.GroupId == group.Id).Select(ToIngredient)]),
            "ingredient group");

    private static RecipeIngredient ToIngredient(RecipeIngredientRow row) =>
        Unwrap(
            RecipeIngredient.Create(
                row.Id,
                row.SortOrder,
                Unwrap(Quantity.Create(row.Quantity, RecipeCodes.ToUnit(row.Unit)), "quantity"),
                row.Name,
                row.Note),
            "ingredient");

    private static Step ToStep(
        StepRow row,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> uses) =>
        Unwrap(
            Step.Create(
                row.Id,
                row.SortOrder,
                Unwrap(StepText.Parse(row.Body), "step text"),
                uses.GetValueOrDefault(row.Id, []),
                row.DurationSeconds,
                row.Title),
            "step");

    private static void Expect(Result result, string what) =>
        result.Match(
            () => { },
            error => throw new InvalidOperationException(
                $"Stored {what} is not valid ({error.Code}). The rows are corrupt."));

    private static TValue Unwrap<TValue>(Result<TValue> result, string what)
        where TValue : notnull =>
        result.Match(
            value => value,
            error => throw new InvalidOperationException(
                $"Stored {what} is not valid ({error.Code}). The row is corrupt."));
}
