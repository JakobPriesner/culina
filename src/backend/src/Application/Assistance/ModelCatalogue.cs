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

    /// <summary>
    /// The newest first.
    /// </summary>
    /// <param name="offered">What the provider listed.</param>
    /// <remarks>
    /// <para>
    /// What somebody opens this list for is usually the model that came out
    /// last week, and alphabetical order buries it: <c>gpt-image-2.5-flare</c>
    /// sorts above <c>gpt-6-astra</c>, and every dated snapshot of a family
    /// sorts next to the family whether it is a year old or a day.
    /// </para>
    /// <para>
    /// A provider that dates nothing keeps name order rather than being given
    /// an order that looks meaningful and is not. The two never mix within one
    /// listing, because a provider either dates its catalogue or does not.
    /// </para>
    /// </remarks>
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
