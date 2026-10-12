namespace Domain.Ingredients;

/// <summary>
/// An ingredient name read into the thing it names and the words about it: "2 große grüne Spargel, geschält"
/// is <em>spargel</em>, qualified <em>green</em>, described <em>large</em>.
/// </summary>
/// <remarks>Words are folded the ä → ae way. Read by <see cref="IngredientWords.Read"/>.</remarks>
/// <param name="Head">The thing, in written order: "kokosmilch". Every word the vocabulary does not know is here.</param>
/// <param name="Qualifiers">What changes which one you buy, as keys in written order: "light", "green".</param>
/// <param name="Descriptors">What changes neither the thing nor which one: "large", "organic".</param>
/// <param name="Preparation">What is done with it in the kitchen, which belongs in the note: "gehackt".</param>
public sealed record IngredientReading(
    IReadOnlyList<string> Head,
    IReadOnlyList<string> Qualifiers,
    IReadOnlyList<string> Descriptors,
    IReadOnlyList<string> Preparation)
{
    /// <summary>
    /// Whether the two are one thing to buy: the same head, give or take an ending ("Tomate", "Tomaten"), and the
    /// same qualifiers in any order. Descriptors and preparation do not count: "große Zwiebel" is a Zwiebel.
    /// </summary>
    /// <param name="other">The other reading.</param>
    public bool IsSameIngredient(IngredientReading other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return HasSameQualifiers(other) && HasSameHead(other);
    }

    /// <summary>
    /// Whether the two name one thing, of whatever kind: "grüner Spargel" and "Spargel" do, "Kokosmilch" and
    /// "Kokosraspel" do not.
    /// </summary>
    /// <param name="other">The other reading.</param>
    public bool HasSameHead(IngredientReading other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Head.Count == other.Head.Count &&
               Head.Zip(other.Head).All(pair => IngredientWords.AreOneWord(pair.First, pair.Second));
    }

    /// <summary>Whether the two are the same kind of their thing: both green, both light, or both plain.</summary>
    /// <param name="other">The other reading.</param>
    public bool HasSameQualifiers(IngredientReading other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Qualifiers.Order(StringComparer.Ordinal).SequenceEqual(other.Qualifiers.Order(StringComparer.Ordinal));
    }
}
