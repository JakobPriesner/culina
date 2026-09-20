using Domain.Import;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.Sources;

/// <summary>
/// Turns a recipe from somebody else's app into one of this app's recipes.
/// </summary>
/// <remarks>
/// <para>
/// Forgiving on purpose, and this is the one place in the codebase where that
/// is the right answer. Everywhere else a value that does not fit the model is
/// a mistake somebody just made and can fix — a form is open and they are
/// looking at it. Here, the values come from a library of eight hundred recipes
/// written over five years by somebody who was not thinking about this app's
/// rules, and there is nobody to ask.
/// </para>
/// <para>
/// So nothing here refuses a recipe over a detail. An ingredient name of 140
/// characters is shortened, a unit this app cannot spell becomes part of the
/// note, an implausible cooking time is dropped. Refusing instead would mean an
/// import that stops on recipe 341 with "invalid unit" — which is technically
/// correct and completely useless.
/// </para>
/// <para>
/// The one thing it will not do is invent. A serving count that cannot be read
/// becomes the default rather than a guess, because that number scales every
/// amount in the recipe, and a wrong one is worse than an absent one.
/// </para>
/// </remarks>
internal static class SourceRecipeMapping
{
    /// <summary>More tags than any recipe carries meaningfully.</summary>
    private const int MaxTags = 25;

    /// <summary>Longer than any introduction, and short of a pasted article.</summary>
    private const int MaxDescriptionLength = 4000;

    /// <summary>
    /// Everything about a recipe except its ingredients and steps.
    /// </summary>
    /// <param name="source">The recipe over there.</param>
    /// <param name="language">The language of the person bringing it over.</param>
    internal static Result<RecipeDetails> ToDetails(SourceRecipe source, Language language)
    {
        ArgumentNullException.ThrowIfNull(source);

        // The one hard requirement. A recipe with no name is not a recipe that
        // can be shortened into one.
        return RecipeTitle.Create(Shorten(source.Title, RecipeTitle.MaxLength))
            .Map(title => new RecipeDetails(
                title,
                Shorten(source.Description, MaxDescriptionLength),
                // Still not guessed from the words — reading "Mehl" as German
                // because it looks German is the kind of cleverness that gets
                // one recipe in twenty wrong with no way to notice. Taken from
                // the person instead: somebody connecting their own library is
                // almost always bringing over recipes in the language they
                // read, and English for all of them was a guess too.
                language,
                ToYield(source.Servings),
                Minutes(source.PrepMinutes),
                Minutes(source.CookMinutes),
                ToTags(source.Tags)));
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

                // Dropped rather than failed: an ingredient with no name is a
                // blank row somebody left behind over there, and it is not
                // worth refusing their recipe over. Recorded either way, so a
                // step that points at the row after it still points at the row
                // after it.
                landed.Add(ToIngredient(line, ingredients.Count).Match(
                    ingredient =>
                    {
                        ingredients.Add(ingredient);
                        taken += 1;

                        return (Guid?)ingredient.Id;
                    },
                    _ => null));
            }

            // An empty group would be a heading with nothing under it.
            if (ingredients.Count == 0)
            {
                continue;
            }

            var made = IngredientGroup.Create(
                id: null,
                Shorten(group.Name, IngredientGroup.MaxNameLength),
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

        // Every recipe has a group, even an empty one: it is the list people
        // type into, and a recipe with none has nowhere to put the first line.
        IReadOnlyList<IngredientGroup> filled =
            groups.Count == 0 ? [IngredientGroup.Implicit()] : groups;

        return Result<ImportedIngredients>.Success(new ImportedIngredients(filled, landed));
    }

    /// <summary>
    /// The steps, with the references the other app made kept as references.
    /// </summary>
    /// <param name="source">The recipe over there.</param>
    /// <param name="landed">
    /// Where each of the source's ingredients ended up, from
    /// <see cref="ToGroups"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// A step is linked to an ingredient only where the other app linked it.
    /// Nothing here reads the words: matching "Mehl" in a sentence against an
    /// ingredient called "Mehl, gesiebt" is exactly the silent guessing the
    /// editor deliberately stopped doing — done here, it would be wrong
    /// invisibly, eight hundred times.
    /// </para>
    /// <para>
    /// A reference that was made over there is not a guess, though. It is the
    /// same fact this app stores, written by the same person, and carrying it
    /// across is what keeps the amounts in an imported step moving with the
    /// servings.
    /// </para>
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
    /// One step's segments, with each reference resolved to a real ingredient.
    /// </summary>
    /// <remarks>
    /// A reference that resolves to nothing is dropped rather than written out
    /// as words. It points at a row this app did not keep — one with no name,
    /// or one past the ingredient limit — and a sentence missing a noun reads
    /// better than one naming something the list does not contain.
    /// </remarks>
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

    /// <summary>
    /// The step's segments with the whitespace at its two ends removed.
    /// </summary>
    /// <remarks>
    /// The two ends only. A space between two words is still a space when one
    /// of the words is a reference to an ingredient.
    /// </remarks>
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
    /// As much of the step as the column holds.
    /// </summary>
    /// <remarks>
    /// Measured in the stored form, because that is what the limit is on and a
    /// reference costs far more stored than it does written. Shortened rather
    /// than refused, for the same reason everything else here is: a step of
    /// four thousand characters is an outlier, and one is not worth losing the
    /// recipe over.
    /// </remarks>
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
                && Shorten(text.Value, limit - StepText.Serialise(kept).Length) is { } cut)
            {
                kept.Add(new TextSegment(cut));
            }

            break;
        }

        // A literal "[[" in the words grows by a character when it is stored,
        // so the cut above can still land a hair over the line.
        while (kept.Count > 0 && StepText.Serialise(kept).Length > limit)
        {
            kept.RemoveAt(kept.Count - 1);
        }

        return kept;
    }

    private static Result<RecipeIngredient> ToIngredient(SourceIngredient line, int sortOrder)
    {
        // A unit this app cannot spell — "1/2 Dose", "some" — is kept as words
        // rather than thrown away. The amount is still right, and the note is
        // where a cook will read it.
        var unit = Unit.Create(line.Unit).Match(value => value, _ => (Unit?)null);
        var strayUnit = unit is null && !string.IsNullOrWhiteSpace(line.Unit) ? line.Unit!.Trim() : null;

        var quantity = Quantity.Create(Amount(line.Amount), unit)
            .Match(value => value, _ => Quantity.Unmeasured);

        var note = Join(strayUnit, line.Note);

        return RecipeIngredient.Create(
            id: null,
            sortOrder,
            quantity,
            Shorten(line.Name, RecipeIngredient.MaxNameLength),
            Shorten(note, RecipeIngredient.MaxNoteLength));
    }

    /// <summary>What it makes, or the default when the number is not usable.</summary>
    private static Yield ToYield(decimal? servings) =>
        servings is null or <= 0 or > Yield.MaxAmount
            ? Yield.Default
            // Servings rather than pieces. Which of the two a number means is
            // something the other app does not record, and portions is what it
            // is far more often.
            : Yield.Create(servings.Value, YieldKind.Servings).Match(value => value, _ => Yield.Default);

    private static IReadOnlyList<string> ToTags(IReadOnlyList<string> tags) =>
    [
        .. tags
            .Select(tag => tag?.Trim())
            .Where(tag => !string.IsNullOrEmpty(tag))
            .Select(tag => tag!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTags)
    ];

    /// <summary>A duration, or nothing when it is not one.</summary>
    private static int? Minutes(int? value) =>
        value is null or <= 0 or > Recipe.MaxMinutes ? null : value;

    private static int? Seconds(int? value) =>
        value is null or <= 0 or > Step.MaxDurationSeconds ? null : value;

    private static decimal? Amount(decimal? value) =>
        value is null or <= 0 or > Quantity.MaxAmount ? null : value;

    /// <summary>
    /// Shortens rather than refuses, and returns null for nothing at all.
    /// </summary>
    /// <remarks>
    /// Cut at a word boundary when there is one near the end, because a name
    /// that stops mid-word reads as corruption while one that stops between
    /// words reads as a long name.
    /// </remarks>
    private static string? Shorten(string? value, int limit)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length <= limit)
        {
            return trimmed;
        }

        var cut = trimmed[..limit];
        var lastSpace = cut.LastIndexOf(' ');

        return (lastSpace > limit - 20 ? cut[..lastSpace] : cut).TrimEnd();
    }

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

/// <summary>
/// The recipe's ingredient list, and a way back to the other app's.
/// </summary>
/// <param name="Groups">The list as this app will have it.</param>
/// <param name="Landed">
/// One entry for each of the source's ingredients, in the source's own order:
/// the id of the line it became, or null where the row was dropped. It is what
/// turns "the third ingredient over there" — which is all a step's reference
/// ever says — into one of this recipe's own ingredients.
/// </param>
internal sealed record ImportedIngredients(
    IReadOnlyList<IngredientGroup> Groups,
    IReadOnlyList<Guid?> Landed);
