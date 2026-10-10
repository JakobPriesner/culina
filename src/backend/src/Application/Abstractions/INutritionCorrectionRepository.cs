namespace Application.Abstractions;

/// <summary>Where a household's choices of what an ingredient really is are kept.</summary>
public interface INutritionCorrectionRepository
{
    /// <summary>
    /// The household's choices for these folded names: a BLS code, or null for "do not count". Names
    /// without a choice are absent.
    /// </summary>
    Task<IReadOnlyDictionary<string, string?>> ForNamesAsync(
        Guid householdId,
        IReadOnlyCollection<string> nameKeys,
        CancellationToken cancellationToken);

    /// <summary>Remembers the choice for a folded name, replacing any earlier one.</summary>
    Task SetAsync(
        Guid householdId,
        string nameKey,
        string? foodCode,
        CancellationToken cancellationToken);

    /// <summary>Forgets the choice for a folded name; nothing to forget is fine.</summary>
    Task RemoveAsync(Guid householdId, string nameKey, CancellationToken cancellationToken);
}
