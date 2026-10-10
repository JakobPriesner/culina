using Application.Abstractions;
using Application.Recipes;
using Contracts.Recipes.Drafts;
using Domain.Import;
using Domain.Recipes;

namespace Application.Assistance;

/// <summary>
/// Turns what a model said into a draft somebody can read. Lenient on purpose: nothing is written down here,
/// so an unusable unit is dropped, a nameless line or wordless step is dropped, and only an answer with nothing in it is refused.
/// </summary>
internal static class DraftMapping
{
    // How long a step's text may be before it is certainly not a step.
    private const int LongestStep = 4000;

    // Whether there is enough here to be worth showing.
    internal static bool IsUsable(this DraftedRecipe draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return !string.IsNullOrWhiteSpace(draft.Title)
            || draft.Groups.Any(group => group.Ingredients.Count > 0)
            || draft.Steps.Count > 0;
    }

    // Turns it into the shape the client reads, under a new id: two asks are two drafts.
    internal static Response ToResponse(this DraftedRecipe draft) =>
        draft.ToResponse(Guid.CreateVersion7());

    // As above under the draft's own id: every piece of a streamed draft is the same draft growing.
    internal static Response ToResponse(this DraftedRecipe draft, Guid draftId)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return new Response
        {
            DraftId = draftId,
            Title = Trimmed(draft.Title),
            Description = RecipeFit.Description(draft.Description),
            YieldAmount = draft.YieldAmount is > 0 ? draft.YieldAmount : null,
            YieldKind = RecipeWords.Of(YieldText.KindOf(draft.YieldAmount, draft.YieldLabel)),
            YieldLabel = Trimmed(draft.YieldLabel),
            PrepMinutes = Minutes(draft.PrepMinutes),
            CookMinutes = Minutes(draft.CookMinutes),
            Groups = [.. draft.Groups.Select(ToGroup).Where(group => group.Ingredients.Count > 0)],
            Steps = [.. draft.Steps.Select(ToStep).OfType<DraftStepContract>()],
            Tags = [.. RecipeFit.Tags(draft.Tags)]
        };
    }

    private static DraftGroupContract ToGroup(DraftedGroup group) => new()
    {
        Name = Trimmed(group.Name),
        Ingredients = [.. group.Ingredients.Select(ToIngredient).OfType<DraftIngredientContract>()]
    };

    // One line, or nothing without a noun: an amount with no ingredient is a mistake the person could not fix.
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

    // The unit if the domain could store it, else nothing: drops "200g" arriving as a unit and keeps the 200.
    private static string? Usable(string? unit) =>
        string.IsNullOrWhiteSpace(unit)
            ? null
            : Unit.Create(unit).Match<string?>(measure => measure.Code, _ => null);

    // One step, or nothing when it says nothing.
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

    // Times outside what a recipe can hold are dropped, not clamped, so nonsense never becomes a plausible number.
    private static int? Minutes(int? value) => value is > 0 and <= Recipe.MaxMinutes ? value : null;

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
