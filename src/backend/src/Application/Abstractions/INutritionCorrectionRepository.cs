namespace Application.Abstractions;

/// <summary>Where a household's choices of what an ingredient really is are kept.</summary>
public interface INutritionCorrectionRepository
{
    /// <summary>
    /// The choices that apply in a library for these folded names, or for every name when null: a household's
    /// own choice, else the nearest one it inherits. Names nobody in the library chose are absent.
    /// </summary>
    /// <param name="library">The household first, then what it inherits, nearest first.</param>
    /// <param name="nameKeys">The folded names; null for all of them.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<IReadOnlyDictionary<string, FoodChoice>> ForNamesAsync(
        IReadOnlyList<Guid> library,
        IReadOnlyCollection<string>? nameKeys,
        CancellationToken cancellationToken);

    /// <summary>Every choice the household itself has made, by folded name: a BLS code, or null for "do not count".</summary>
    Task<IReadOnlyDictionary<string, string?>> AllAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>Remembers the choice for a folded name, replacing any earlier one.</summary>
    Task SetAsync(
        Guid householdId,
        string nameKey,
        string? foodCode,
        CancellationToken cancellationToken);

    /// <summary>Forgets the choice for a folded name; nothing to forget is fine.</summary>
    Task RemoveAsync(Guid householdId, string nameKey, CancellationToken cancellationToken);
}

/// <summary>What an ingredient name counts as in a library, and which household said so.</summary>
/// <param name="FoodCode">A BLS code, or null for "do not count".</param>
/// <param name="HouseholdId">The household whose choice it is: the reader's own, or one it inherits from.</param>
public sealed record FoodChoice(string? FoodCode, Guid HouseholdId);
