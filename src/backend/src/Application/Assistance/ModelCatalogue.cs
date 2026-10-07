using Application.Abstractions;

namespace Application.Assistance;

/// <summary>Narrowing a provider's catalogue to what a recipe app can use.</summary>
/// <remarks>
/// The name-based filter is a guess, so it never empties the list: a key that reaches only speech models should show them, which exposes the wrong permissions.
/// </remarks>
public static class ModelCatalogue
{
    /// <summary>The models worth offering, or all of them if that would be none.</summary>
    /// <param name="offered">Everything the provider listed.</param>
    /// <param name="worthOffering">Whether this one can do the app's work.</param>
    public static IReadOnlyList<ModelInfo> Narrow(
        IReadOnlyList<ModelInfo> offered,
        Func<ModelInfo, bool> worthOffering)
    {
        ArgumentNullException.ThrowIfNull(offered);
        ArgumentNullException.ThrowIfNull(worthOffering);

        var kept = offered.Where(worthOffering).ToList();

        return kept.Count > 0 ? kept : offered;
    }

    /// <summary>The newest first, since alphabetical order buries last week's model; undated providers keep name order.</summary>
    /// <param name="offered">What the provider listed.</param>
    public static IReadOnlyList<ModelInfo> Newest(IReadOnlyList<ModelInfo> offered)
    {
        ArgumentNullException.ThrowIfNull(offered);

        return
        [
            .. offered
                .OrderByDescending(model => model.Added ?? DateTimeOffset.MinValue)
                .ThenBy(model => model.Id, StringComparer.Ordinal)
        ];
    }
}
