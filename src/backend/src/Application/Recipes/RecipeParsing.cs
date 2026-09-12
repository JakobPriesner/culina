using Application.Recipes.Update;
using Contracts.Recipes;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes;

/// <summary>
/// Turns the wire shape into domain objects, naming the field that was wrong.
/// </summary>
/// <remarks>
/// Every failure is reported with the request field it came from, so a form can
/// mark the right control rather than showing one general message over a
/// twenty-field editor.
/// </remarks>
internal static class RecipeParsing
{
    internal static Result<RecipeDetails> ToDetails(RecipeDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var title = RecipeTitle.Create(draft.Title);
        var language = RecipeWords.ToLanguage(draft.Language);
        var kind = RecipeWords.ToYieldKind(draft.YieldKind);

        return Result.Combine(
                Labelled(title, "title"),
                Ignoring(language),
                Ignoring(kind))
            .Bind(() => kind.Bind(yieldKind => Yield.Create(draft.YieldAmount, yieldKind)))
            .Bind(measure => title.Bind(value => language.Map(code => new RecipeDetails(
                value,
                draft.Description,
                code,
                measure,
                draft.PrepMinutes,
                draft.CookMinutes,
                draft.Tags))));
    }

    internal static Result<IReadOnlyList<IngredientGroup>> ToGroups(
        IReadOnlyList<IngredientGroupContract> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        return groups.Select(ToGroup).Collect();
    }

    internal static Result<IReadOnlyList<Step>> ToSteps(IReadOnlyList<StepContract> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        return steps.Select(ToStep).Collect();
    }

    /// <summary>
    /// Sort order comes from position in the request, not from a field the
    /// client sends: the list the user sees is the order, and a separate field
    /// is one more thing that can disagree with it.
    /// </summary>
    private static Result<Step> ToStep(StepContract step, int sortOrder) =>
        Step.Create(
            step.StepId,
            sortOrder,
            [.. step.Segments.Select(ToSegment)],
            step.DurationSeconds);

    private static Result<IngredientGroup> ToGroup(IngredientGroupContract group, int sortOrder) =>
        group.Ingredients.Select(ToIngredient).Collect()
            .Bind(ingredients => IngredientGroup.Create(
                group.GroupId,
                group.Name,
                sortOrder,
                ingredients));

    private static Result<RecipeIngredient> ToIngredient(IngredientContract line, int sortOrder) =>
        RecipeWords.ToQuantity(line.Quantity, line.Unit)
            .Bind(amount => RecipeIngredient.Create(
                line.IngredientId,
                sortOrder,
                amount,
                line.Name,
                line.Note));

    private static StepSegment ToSegment(StepSegmentContract segment) =>
        segment.Type == RecipeMappings.IngredientSegmentType && segment.RecipeIngredientId is { } id
            ? new IngredientSegment(id)
            : new TextSegment(segment.Value ?? string.Empty);

    private static Result Labelled<TValue>(Result<TValue> result, string field)
        where TValue : notnull =>
        result.Match(
            _ => Result.Success(),
            error => Result.Failure(new FieldError(field, error.Code, error.Description)));

    private static Result Ignoring<TValue>(Result<TValue> result)
        where TValue : notnull =>
        result.Match(_ => Result.Success(), Result.Failure);
}
