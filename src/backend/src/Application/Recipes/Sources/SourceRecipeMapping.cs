using Domain.Import;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.Sources;

/// <summary>Turns a recipe from somebody else's app into one of this app's recipes.</summary>
/// <remarks>
/// Forgiving on purpose: values come from a large library nobody can be asked about, so details are
/// shortened or dropped rather than refused. It never invents: an unreadable serving count becomes
/// the default, since it scales every amount.
/// </remarks>
internal static class SourceRecipeMapping
{
    /// <summary>Everything about a recipe except its ingredients and steps.</summary>
    /// <param name="source">The recipe over there.</param>
    /// <param name="language">The language of the person bringing it over.</param>
    internal static Result<RecipeDetails> ToDetails(SourceRecipe source, Language language)
    {
        ArgumentNullException.ThrowIfNull(source);

        // The one hard requirement: a recipe with no name cannot be shortened into one.
        return RecipeTitle.Create(RecipeFit.Shorten(source.Title, RecipeTitle.MaxLength))
            .Map(title => new RecipeDetails(
                title,
                RecipeFit.Description(source.Description),
                // The importer's language; guessing from the words would be silently wrong for some
                // recipes.
                language,
                ToYield(source.Servings),
                Minutes(source.PrepMinutes),
                Minutes(source.CookMinutes),
                RecipeFit.Tags(source.Tags)));
    }

    /// <summary>The ingredients, grouped as they were grouped over there.</summary>
    /// <param name="source">The recipe over there.</param>
    internal static Result<ImportedIngredients> ToGroups(SourceRecipe source)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<IngredientGroup> groups = [];
        List<Guid?> landed = [];
        var taken = 0;

        foreach (var group in source.Groups)
        {
            List<RecipeIngredient> ingredients = [];

            foreach (var line in group.Ingredients)
            {
                if (taken == Recipe.MaxIngredients)
                {
                    break;
                }

                // Dropped, not failed: a nameless row is a blank left behind. Still recorded in
                // `landed`, so later step references keep their indexes.
                landed.Add(ToIngredient(line, ingredients.Count).Match(
                    ingredient =>
                    {
                        ingredients.Add(ingredient);
                        taken += 1;

                        return (Guid?)ingredient.Id;
                    },
                    _ => null));
            }

            if (ingredients.Count == 0)
            {
                continue;
            }

            var made = IngredientGroup.Create(
                id: null,
                RecipeFit.Shorten(group.Name, IngredientGroup.MaxNameLength),
                groups.Count,
                ingredients);

            var failed = made.Match(
                value =>
                {
                    groups.Add(value);

                    return false;
                },
                _ => true);

            if (failed)
            {
                return RecipeErrors.InvalidIngredientName;
            }
        }

        // Every recipe has a group, even an empty one: it is the list people type into.
        IReadOnlyList<IngredientGroup> filled =
            groups.Count == 0 ? [IngredientGroup.Implicit()] : groups;

        return Result<ImportedIngredients>.Success(new ImportedIngredients(filled, landed));
    }

    /// <summary>The steps, with references made in the other app kept as references.</summary>
    /// <param name="source">The recipe over there.</param>
    /// <param name="landed">
    /// Where each source ingredient ended up, from <see cref="ToGroups"/>.
    /// </param>
    /// <remarks>
    /// Nothing reads the words: matching "Mehl" against "Mehl, gesiebt" is the silent guessing the
    /// editor stopped doing. A reference made over there is not a guess.
    /// </remarks>
    internal static Result<IReadOnlyList<Step>> ToSteps(
        SourceRecipe source,
        IReadOnlyList<Guid?> landed)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(landed);

        List<Step> steps = [];

        foreach (var step in source.Steps.Take(Recipe.MaxSteps))
        {
            var segments = Fit(Trim(ToSegments(step, landed)), Step.MaxTextLength);

            if (segments.Count == 0)
            {
                continue;
            }

            var made = Step.Create(
                id: null,
                steps.Count,
                segments,
                uses: [],
                Seconds(step.Seconds));

            var failed = made.Match(
                value =>
                {
                    steps.Add(value);

                    return false;
                },
                _ => true);

            if (failed)
            {
                return RecipeErrors.InvalidStepText;
            }
        }

        return Result<IReadOnlyList<Step>>.Success(steps);
    }

    /// <summary>
    /// One step's segments, with each reference resolved; one that resolves to nothing is dropped.
    /// </summary>
    private static List<StepSegment> ToSegments(SourceStep step, IReadOnlyList<Guid?> landed)
    {
        List<StepSegment> segments = [];

        foreach (var segment in step.Segments)
        {
            switch (segment)
            {
                case SourceTextSegment text when text.Value.Length > 0:
                    segments.Add(new TextSegment(text.Value));
                    break;

                case SourceIngredientReference reference
                    when reference.Index >= 0
                        && reference.Index < landed.Count
                        && landed[reference.Index] is { } ingredientId:
                    segments.Add(new IngredientSegment(ingredientId));
                    break;

                default:
                    break;
            }
        }

        return segments;
    }

    /// <summary>The step's segments with whitespace removed from its two ends only.</summary>
    private static List<StepSegment> Trim(List<StepSegment> segments)
    {
        if (segments is [TextSegment first, ..])
        {
            segments[0] = new TextSegment(first.Value.TrimStart());
        }

        if (segments is [.., TextSegment last])
        {
            segments[^1] = new TextSegment(last.Value.TrimEnd());
        }

        return [.. segments.Where(segment => segment is not TextSegment { Value.Length: 0 })];
    }

    /// <summary>
    /// As much of the step as the column holds, measured in stored form (references cost more
    /// stored).
    /// </summary>
    private static List<StepSegment> Fit(List<StepSegment> segments, int limit)
    {
        List<StepSegment> kept = [];

        foreach (var segment in segments)
        {
            kept.Add(segment);

            if (StepText.Serialise(kept).Length <= limit)
            {
                continue;
            }

            kept.RemoveAt(kept.Count - 1);

            if (segment is TextSegment text
                && RecipeFit.Shorten(text.Value, limit - StepText.Serialise(kept).Length) is { } cut)
            {
                kept.Add(new TextSegment(cut));
            }

            break;
        }

        // A literal "[[" grows by a character when stored, so the cut can still land over the
        // limit.
        while (kept.Count > 0 && StepText.Serialise(kept).Length > limit)
        {
            kept.RemoveAt(kept.Count - 1);
        }

        return kept;
    }

    private static Result<RecipeIngredient> ToIngredient(SourceIngredient line, int sortOrder)
    {
        // A unit this app cannot spell ("1/2 Dose") is kept in the note rather than thrown away.
        var unit = Unit.Create(line.Unit).Match(value => value, _ => (Unit?)null);
        var strayUnit = unit is null && !string.IsNullOrWhiteSpace(line.Unit) ? line.Unit!.Trim() : null;

        var quantity = Quantity.Create(Amount(line.Amount), unit)
            .Match(value => value, _ => Quantity.Unmeasured);

        var note = Join(strayUnit, line.Note);

        return RecipeIngredient.Create(
            id: null,
            sortOrder,
            quantity,
            RecipeFit.Shorten(line.Name, RecipeIngredient.MaxNameLength),
            RecipeFit.Shorten(note, RecipeIngredient.MaxNoteLength));
    }

    private static Yield ToYield(decimal? servings) =>
        servings is null or <= 0 or > Yield.MaxAmount
            ? Yield.Default
            // Servings rather than pieces: the other app does not record which, and portions is far
            // more common.
            : Yield.Create(servings.Value, YieldKind.Servings).Match(value => value, _ => Yield.Default);

    private static int? Minutes(int? value) =>
        value is null or <= 0 or > Recipe.MaxMinutes ? null : value;

    private static int? Seconds(int? value) =>
        value is null or <= 0 or > Step.MaxDurationSeconds ? null : value;

    private static decimal? Amount(decimal? value) =>
        value is null or <= 0 or > Quantity.MaxAmount ? null : value;

    private static string? Join(string? first, string? second)
    {
        var left = first?.Trim();
        var right = second?.Trim();

        if (string.IsNullOrEmpty(left))
        {
            return right;
        }

        return string.IsNullOrEmpty(right) ? left : $"{left}, {right}";
    }
}

/// <summary>The recipe's ingredient list, and a way back to the other app's.</summary>
/// <param name="Groups">The list as this app will have it.</param>
/// <param name="Landed">
/// The id of the line each source ingredient became (null if dropped), in source order; it turns a
/// step's "third ingredient over there" into one of this recipe's own.
/// </param>
internal sealed record ImportedIngredients(
    IReadOnlyList<IngredientGroup> Groups,
    IReadOnlyList<Guid?> Landed);
