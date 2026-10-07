using Domain.Shared;
using Domain.Shopping;

namespace Application.Abstractions;

/// <summary>Where a household's shopping list is kept.</summary>
public interface IShoppingListRepository
{
    /// <summary>The household's list, created if this is the first time anyone looked.</summary>
    /// <remarks>
    /// Lazily, not on household creation: a row inserted months ago is indistinguishable from a
    /// list somebody wanted.
    /// </remarks>
    Task<Result<ShoppingList>> ForHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>Saves the whole list, guarded by the version the caller had.</summary>
    Task<Result> SaveAsync(
        ShoppingList list,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>Where this household has decided things actually live.</summary>
    Task<IReadOnlyDictionary<string, ShoppingSection>> SectionOverridesAsync(
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Remembers that a thing, by its folded name, is found in another section here.
    /// </summary>
    Task<Result> RememberSectionAsync(
        Guid householdId,
        string nameKey,
        ShoppingSection section,
        CancellationToken cancellationToken);
}
