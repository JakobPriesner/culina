using Domain.Shared;
using Domain.Shopping;

namespace Application.Abstractions;

/// <summary>Where a household's shopping list is kept.</summary>
public interface IShoppingListRepository
{
    /// <summary>
    /// The household's list, created if this is the first time anyone looked.
    /// </summary>
    /// <param name="householdId">Whose list.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <remarks>
    /// Lazily rather than when a household is created: a list that exists
    /// because a row was inserted three months ago is indistinguishable from
    /// one that exists because somebody wanted it.
    /// </remarks>
    Task<Result<ShoppingList>> ForHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>Saves the whole list, guarded by its version.</summary>
    /// <param name="list">The list, already changed.</param>
    /// <param name="expectedVersion">The version the caller had.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> SaveAsync(
        ShoppingList list,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>Where this household has decided things actually live.</summary>
    /// <param name="householdId">Whose corrections.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyDictionary<string, ShoppingSection>> SectionOverridesAsync(
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>Remembers that a thing is found somewhere else here.</summary>
    /// <param name="householdId">Whose correction.</param>
    /// <param name="nameKey">The folded name it applies to.</param>
    /// <param name="section">Where it actually is.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task<Result> RememberSectionAsync(
        Guid householdId,
        string nameKey,
        ShoppingSection section,
        CancellationToken cancellationToken);
}
