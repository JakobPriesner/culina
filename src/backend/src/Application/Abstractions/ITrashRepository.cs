using Domain.Households;

namespace Application.Abstractions;

/// <summary>
/// The bin: what was deleted, putting it back, and removing it for good.
/// </summary>
/// <remarks>
/// The only port that sees deleted rows. Every other repository reads views
/// that do not contain them, which is what keeps a deleted recipe out of every
/// list and search without each one having to remember.
/// </remarks>
public interface ITrashRepository
{
    /// <summary>The recipes and cookbooks in a household's bin, newest first.</summary>
    /// <param name="householdId">Which household; it must not itself be deleted.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<TrashedItem>> ForHouseholdAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>The deleted households this person owns, newest first.</summary>
    /// <param name="userId">Who is asking.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<DeletedHousehold>> DeletedHouseholdsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>A deleted household, with its members, or null when there is no such household in the bin.</summary>
    /// <param name="householdId">Which household.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Household?> DeletedHouseholdAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>
    /// Whose bin a deleted recipe is in, or null when it is in none that can be
    /// opened — never deleted, purged, or in a household that is deleted too.
    /// </summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Guid?> HouseholdOfDeletedRecipeAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>Whose bin a deleted cookbook is in, under the same rule as a recipe.</summary>
    /// <param name="cookbookId">Which cookbook.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<Guid?> HouseholdOfDeletedCookbookAsync(Guid cookbookId, CancellationToken cancellationToken);

    /// <summary>Takes a recipe out of the bin, and makes it findable again.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>False when it was no longer in the bin.</returns>
    Task<bool> RestoreRecipeAsync(Guid recipeId, CancellationToken cancellationToken);

    /// <summary>Takes a cookbook out of the bin.</summary>
    /// <param name="cookbookId">Which cookbook.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>False when it was no longer in the bin.</returns>
    Task<bool> RestoreCookbookAsync(Guid cookbookId, CancellationToken cancellationToken);

    /// <summary>Takes a household out of the bin, with everything it had.</summary>
    /// <param name="householdId">Which household.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>False when it was no longer in the bin.</returns>
    Task<bool> RestoreHouseholdAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes for good everything deleted before <paramref name="cutoff"/>, and
    /// says which image files nothing points at any more.
    /// </summary>
    /// <param name="cutoff">Anything deleted before this goes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<PurgedTrash> PurgeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}

/// <summary>Something in a household's bin.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Id">Its id, for restoring it.</param>
/// <param name="Name">The recipe's title or the cookbook's name.</param>
/// <param name="DeletedAt">When it was deleted.</param>
/// <param name="DeletedBy">Who deleted it, by name, if they still have an account.</param>
public sealed record TrashedItem(TrashedKind Kind, Guid Id, string Name, DateTimeOffset DeletedAt, string? DeletedBy);

/// <summary>A household in the bin.</summary>
/// <param name="Household">The household as it was, members included.</param>
/// <param name="DeletedAt">When it was deleted.</param>
public sealed record DeletedHousehold(Household Household, DateTimeOffset DeletedAt);

/// <summary>What kind of thing is in the bin.</summary>
public enum TrashedKind
{
    /// <summary>A recipe.</summary>
    Recipe = 0,

    /// <summary>A cookbook.</summary>
    Cookbook = 1
}

/// <summary>What a purge removed.</summary>
/// <param name="Removed">How many households, recipes and cookbooks went.</param>
/// <param name="ReleasedImages">Content hashes of image files nothing points at any more.</param>
public sealed record PurgedTrash(int Removed, IReadOnlyList<string> ReleasedImages);
