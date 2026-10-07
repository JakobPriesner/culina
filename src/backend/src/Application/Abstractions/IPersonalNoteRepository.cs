using Domain.Cooking;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes one person's notes on a recipe.</summary>
public interface IPersonalNoteRepository
{
    /// <summary>Every note this person has written on this recipe.</summary>
    Task<IReadOnlyList<PersonalNote>> ForRecipeAsync(
        Guid recipeId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>This person's notes on several recipes, in one round trip.</summary>
    Task<ILookup<Guid, PersonalNote>> ForRecipesAsync(
        IReadOnlyCollection<Guid> recipeIds,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Replaces this person's notes on this recipe with the ones supplied; an empty note means "delete this one".</summary>
    Task<Result> ReplaceAsync(
        Guid recipeId,
        Guid userId,
        IReadOnlyList<PersonalNote> notes,
        CancellationToken cancellationToken);
}
