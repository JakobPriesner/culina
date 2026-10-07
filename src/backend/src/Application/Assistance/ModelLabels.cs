using Application.Abstractions;

namespace Application.Assistance;

/// <summary>Makes a listing's names tell its models apart.</summary>
/// <remarks>
/// Providers reuse one display name across several ids (preview, snapshot, moving alias), and the difference
/// is the reason somebody opens the picker. Only colliding names are changed.
/// </remarks>
public static class ModelLabels
{
    /// <summary>The same models, with the duplicated names spelled out.</summary>
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
