using Domain.Shopping;

namespace Domain.Ingredients;

/// <summary>Every merge a household has made, looked up by the folded name that goes away.</summary>
public sealed class IngredientMerges
{
    private readonly Dictionary<string, string> kept;

    /// <summary>Collects a household's merges.</summary>
    public IngredientMerges(IEnumerable<IngredientMerge> merges)
    {
        ArgumentNullException.ThrowIfNull(merges);

        kept = merges.ToDictionary(merge => merge.From.ComparisonKey, merge => merge.To.Value, StringComparer.Ordinal);
    }

    /// <summary>A household that has merged nothing.</summary>
    public static IngredientMerges None { get; } = new([]);

    /// <summary>Whether there is anything to apply.</summary>
    public bool IsEmpty => kept.Count == 0;

    /// <summary>The name a line written <paramref name="name"/> is stored as: the kept one, or itself.</summary>
    /// <remarks>One step is enough: rules are kept flat, so a kept name is never merged away itself.</remarks>
    public string Resolve(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return kept.TryGetValue(ItemName.Fold(name), out var to) ? to : name;
    }
}
