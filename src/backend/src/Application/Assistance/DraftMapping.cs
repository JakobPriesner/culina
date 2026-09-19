using Application.Abstractions;
using Contracts.Recipes.Drafts;
using Domain.Recipes;

namespace Application.Assistance;

/// <summary>
/// Turns what a model said into a draft somebody can read.
/// </summary>
/// <remarks>
/// <para>
/// Lenient on purpose, and this is the one place in the backend where that is
/// the right answer. Everywhere else a value that cannot become a domain object
/// is a failure, because everywhere else something is about to be written down.
/// Nothing is written down here: a draft is shown back for correction, and
/// refusing a whole recipe because one line said "a splash" would be throwing
/// away nineteen good lines to be right about one.
/// </para>
/// <para>
/// So a quantity that cannot be a quantity loses its unit and keeps its number,
/// a line with no name at all is dropped, and a step with no words is dropped.
/// The result is always something a person can fix by hand, which is what they
/// were going to do anyway.
/// </para>
/// <para>
/// The one refusal left is an answer with nothing in it — no title, no
/// ingredients and no steps. That is not a draft to correct, it is a model that
/// did not answer, and saying so lets somebody ask again.
/// </para>
/// </remarks>
internal static class DraftMapping
{
    /// <summary>How long a step's text may be before it is certainly not a step.</summary>
    private const int LongestStep = 4000;

    /// <summary>Whether there is enough here to be worth showing.</summary>
    /// <param name="draft">What the model said.</param>
    internal static bool IsUsable(this DraftedRecipe draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return !string.IsNullOrWhiteSpace(draft.Title)
            || draft.Groups.Any(group => group.Ingredients.Count > 0)
            || draft.Steps.Count > 0;
    }

    /// <summary>Turns it into the shape the client reads.</summary>
    /// <param name="draft">What the model said.</param>
    internal static Response ToResponse(this DraftedRecipe draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return new Response
        {
            Title = Trimmed(draft.Title),
            Description = Trimmed(draft.Description),
            YieldAmount = draft.YieldAmount is > 0 ? draft.YieldAmount : null,
            YieldLabel = Trimmed(draft.YieldLabel),
            PrepMinutes = Minutes(draft.PrepMinutes),
            CookMinutes = Minutes(draft.CookMinutes),
            Groups = [.. draft.Groups.Select(ToGroup).Where(group => group.Ingredients.Count > 0)],
            Steps = [.. draft.Steps.Select(ToStep).OfType<DraftStepContract>()],
            Tags = [.. draft.Tags
                .Select(Trimmed)
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)]
        };
    }

    private static DraftGroupContract ToGroup(DraftedGroup group) => new()
    {
        Name = Trimmed(group.Name),
        Ingredients = [.. group.Ingredients.Select(ToIngredient).OfType<DraftIngredientContract>()]
    };

    /// <summary>
    /// One line, or nothing when it has no noun.
    /// </summary>
    /// <remarks>
    /// The name is the only part that cannot be missing. A line with an amount
    /// and no ingredient is not a shorter line, it is a mistake — and one the
    /// person correcting the draft could not fix without knowing what the model
    /// meant.
    /// </remarks>
    private static DraftIngredientContract? ToIngredient(DraftedIngredient line)
    {
        if (Trimmed(line.Name) is not { } name)
        {
            return null;
        }

        return new DraftIngredientContract
        {
            Quantity = line.Quantity is > 0 ? line.Quantity : null,
            Unit = Usable(line.Unit),
            Name = name,
            Note = Trimmed(line.Note)
        };
    }

    /// <summary>
    /// The unit if the app could store it, otherwise nothing.
    /// </summary>
    /// <remarks>
    /// Asked of the domain rather than guessed at here, so the rule has one
    /// home. A unit is any word, so most of what a model writes survives; what
    /// does not is the classic "200g" arriving as a unit because the amount
    /// lost its space, and dropping it keeps the 200.
    /// </remarks>
    private static string? Usable(string? unit) =>
        string.IsNullOrWhiteSpace(unit)
            ? null
            : Unit.Create(unit).Match<string?>(measure => measure.Code, _ => null);

    /// <summary>One step, or nothing when it says nothing.</summary>
    private static DraftStepContract? ToStep(DraftedStep step)
    {
        if (Trimmed(step.Text) is not { } text || text.Length > LongestStep)
        {
            return null;
        }

        return new DraftStepContract
        {
            Title = Trimmed(step.Title),
            Text = text,
            DurationSeconds = step.DurationSeconds is > 0 ? step.DurationSeconds : null
        };
    }

    /// <summary>Times outside what a recipe can hold are dropped, not clamped.</summary>
    /// <remarks>
    /// Clamping would turn a model's nonsense into a plausible number somebody
    /// might not check. A blank is obviously a blank.
    /// </remarks>
    private static int? Minutes(int? value) => value is > 0 and <= Recipe.MaxMinutes ? value : null;

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
