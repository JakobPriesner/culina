using Domain.Cooking;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes the record of what someone has cooked.</summary>
public interface ICookLogRepository
{
    /// <summary>This person's entries for one recipe, newest first.</summary>
    Task<IReadOnlyList<CookLogEntry>> ForRecipeAsync(
        Guid recipeId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>This person's entries for several recipes, in one round trip.</summary>
    Task<ILookup<Guid, CookLogEntry>> ForRecipesAsync(
        IReadOnlyCollection<Guid> recipeIds,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Appends an entry.</summary>
    Task<Result> AddAsync(CookLogEntry entry, CancellationToken cancellationToken);

    /// <summary>One of this person's entries, or not found.</summary>
    Task<Result<CookLogEntry>> FindAsync(
        Guid entryId,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Hangs a photo on an entry, or takes the one it has away (null).</summary>
    Task<Result> SetPhotoAsync(
        Guid entryId,
        Guid userId,
        CookPhoto? photo,
        CancellationToken cancellationToken);

    /// <summary>Removes an entry, for the undo behind the "made it" toast.</summary>
    Task<Result> RemoveAsync(Guid entryId, Guid userId, CancellationToken cancellationToken);
}
