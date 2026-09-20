using Application.Abstractions;

namespace Application.Assistance;

/// <summary>
/// Makes a listing's names tell its models apart.
/// </summary>
/// <remarks>
/// <para>
/// Providers reuse a display name across several ids. Google gives every
/// release of one family the same friendly name, so a catalogue holds
/// "Nano Banana Pro" three times — the preview, the dated snapshot, and the
/// alias that follows whichever is current — and a picker built straight from
/// it offers three identical lines.
/// </para>
/// <para>
/// Three lines reading the same is worse than a long name. They are not the
/// same model: one is pinned to a snapshot and one moves under you. Choosing
/// between them is the reason somebody opened this list, and a list that hides
/// the difference makes the choice a guess.
/// </para>
/// <para>
/// Only the names that collide are changed. A model whose display name is
/// already unique keeps it, because the id it is hiding is exactly the noise
/// the display name was there to spare everybody.
/// </para>
/// </remarks>
public static class ModelLabels
{
    /// <summary>
    /// The same models, with the duplicated names spelled out.
    /// </summary>
    /// <param name="listed">What the provider offered, in the order to keep.</param>
    public static IReadOnlyList<ModelInfo> Distinguish(IReadOnlyList<ModelInfo> listed)
    {
        ArgumentNullException.ThrowIfNull(listed);

        var shared = listed
            .GroupBy(model => model.Label, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return shared.Count == 0
            ? listed
            : [.. listed.Select(model => shared.Contains(model.Label) && model.Label != model.Id
                ? model with { Label = $"{model.Label} ({model.Id})" }
                : model)];
    }
}
