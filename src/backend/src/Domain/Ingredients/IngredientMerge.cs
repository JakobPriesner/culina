using Domain.Shared;
using Domain.Shopping;

namespace Domain.Ingredients;

/// <summary>
/// A household's standing decision that one ingredient name is another: every line written
/// <see cref="From"/> is stored as <see cref="To"/>.
/// </summary>
/// <remarks>
/// Both names fold like shopping items, so the rule covers every spelling of <see cref="From"/>
/// ("Olivenoel", "olivenöl"). A rule may fold to its own target: that settles a spelling.
/// </remarks>
public sealed record IngredientMerge
{
    private IngredientMerge(ItemName from, ItemName to)
    {
        From = from;
        To = to;
    }

    /// <summary>The name that goes away.</summary>
    public ItemName From { get; }

    /// <summary>The name that is kept.</summary>
    public ItemName To { get; }

    /// <summary>Reads a merge, or says why it is not one.</summary>
    /// <param name="from">The name that goes away.</param>
    /// <param name="to">The name that is kept.</param>
    public static Result<IngredientMerge> Create(string? from, string? to)
    {
        var fromName = ItemName.Create(from);
        var toName = ItemName.Create(to);

        return fromName.Match(
            one => toName.Match(
                other => one.Value == other.Value
                    ? IngredientErrors.SameName
                    : Result<IngredientMerge>.Success(new IngredientMerge(one, other)),
                _ => IngredientErrors.InvalidName),
            _ => IngredientErrors.InvalidName);
    }
}
