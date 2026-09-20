using Application.Abstractions;

namespace Application.Assistance;

/// <summary>
/// Narrowing a provider's catalogue to what a recipe app can use.
/// </summary>
/// <remarks>
/// <para>
/// The adapters drop the models that plainly cannot write a recipe —
/// embeddings, speech, moderation — because a catalogue of forty is only worth
/// reading once the twenty that cannot answer are out of it. The rule is a
/// reading of names, though, and a reading of names is a guess.
/// </para>
/// <para>
/// So it never empties the list. A picker with nothing in it tells an
/// administrator that their provider offered nothing, which is a different
/// thing from what happened and sends them to look in the wrong place: a key
/// that can reach only speech models is a key whose permissions are wrong, and
/// the six speech models on screen say so immediately. Hiding them says nothing
/// at all.
/// </para>
/// </remarks>
public static class ModelCatalogue
{
    /// <summary>
    /// The models worth offering, or all of them if that would be none.
    /// </summary>
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
}
