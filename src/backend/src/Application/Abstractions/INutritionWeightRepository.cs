namespace Application.Abstractions;

/// <summary>Where a household's own weights per ingredient and unit, and whether typical weights count, are kept.</summary>
public interface INutritionWeightRepository
{
    /// <summary>
    /// The household's weights for these folded names, as grams by unit key (see <c>UnitKeys</c>), keyed by
    /// name. Names without a weight are absent.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>> ForNamesAsync(
        Guid householdId,
        IReadOnlyCollection<string> nameKeys,
        CancellationToken cancellationToken);

    /// <summary>Every weight the household has set, by folded name, then unit key.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>>> AllAsync(
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>Remembers what one unit of a folded name weighs, replacing any earlier weight.</summary>
    Task SetAsync(Guid householdId, string nameKey, string unitKey, decimal grams, CancellationToken cancellationToken);

    /// <summary>Forgets the weight for a folded name and unit; nothing to forget is fine.</summary>
    Task RemoveAsync(Guid householdId, string nameKey, string unitKey, CancellationToken cancellationToken);

    /// <summary>Whether typical weights may count lines for this household; true until it says otherwise.</summary>
    Task<bool> UsesTypicalWeightsAsync(Guid householdId, CancellationToken cancellationToken);

    /// <summary>Remembers whether typical weights may count lines for this household.</summary>
    Task SetUsesTypicalWeightsAsync(Guid householdId, bool use, CancellationToken cancellationToken);
}
