namespace Application.Abstractions;

/// <summary>Where a household's own weights per ingredient and unit, and whether typical weights count, are kept.</summary>
public interface INutritionWeightRepository
{
    /// <summary>
    /// The weights that apply in a library for these folded names, or for every name when null, by unit key
    /// (see <c>UnitKeys</c>): a household's own weight, else the nearest one it inherits. Names without a
    /// weight are absent.
    /// </summary>
    /// <param name="library">The household first, then what it inherits, nearest first.</param>
    /// <param name="nameKeys">The folded names; null for all of them.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, WeightChoice>>> ForNamesAsync(
        IReadOnlyList<Guid> library,
        IReadOnlyCollection<string>? nameKeys,
        CancellationToken cancellationToken);

    /// <summary>Every weight the household itself has set, by folded name, then unit key.</summary>
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

/// <summary>What one unit of an ingredient weighs in a library, and which household said so.</summary>
/// <param name="Grams">Grams of one unit.</param>
/// <param name="HouseholdId">The household whose weight it is: the reader's own, or one it inherits from.</param>
public sealed record WeightChoice(decimal Grams, Guid HouseholdId);
