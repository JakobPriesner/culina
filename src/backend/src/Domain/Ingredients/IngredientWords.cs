using Domain.Nutrition;
using Domain.Search;

namespace Domain.Ingredients;

/// <summary>What a word in an ingredient name does, when it is not the thing itself.</summary>
public enum IngredientWordKind
{
    /// <summary>Changes which one you buy: "grüner" Spargel, Kokosmilch "light", "TK"-Erbsen.</summary>
    Qualifier,

    /// <summary>Changes neither the thing nor which one: "große" Zwiebel, "Bio"-Eier.</summary>
    Descriptor,

    /// <summary>What is done with it in the kitchen, which belongs in the note: "gehackte" Zwiebeln.</summary>
    Preparation
}

/// <summary>
/// The small vocabulary that reads an ingredient name into the thing and the words about it, in German and English.
/// </summary>
/// <remarks>
/// <para>
/// Curated, like <see cref="CulinaryLexicon"/>, because whether a word changes the purchase is a matter of
/// cooking, not grammar: "gehackte Zwiebeln" is preparation, "gehackte Tomaten" a can on the shelf. So a word is
/// only taken off the thing when <see cref="FoodNames"/> does not know the whole name as another food.
/// </para>
/// <para>
/// A word the vocabulary does not know stays part of the thing, which is how every name was read before, so the
/// worst a missing word does is keep two names apart. The frontend's line parser mirrors the qualifier forms
/// (<c>features/recipes/editor/qualifierWords.ts</c>).
/// </para>
/// </remarks>
public static class IngredientWords
{
    /// <summary>The shortest a word may be and still differ from another only by an ending: not "Butt" and "Butter".</summary>
    private const int ShortestStem = 5;

    /// <summary>The shortest food a compound may end in and still be split: "Rot·wein", not "Schwarz·tee".</summary>
    private const int ShortestCompoundRest = 4;

    /// <summary>The longest phrase in the vocabulary, in words: "aus der dose".</summary>
    private const int LongestPhrase = 3;

    /// <summary>What a German adjective or participle grows by when declined: grün, grüne, grüner, grünes, grünen, grünem.</summary>
    private static readonly string[] Declensions = ["", "e", "er", "es", "en", "em"];

    /// <summary>What a noun grows by in the plural: Tomate(n), Zwiebel(n), Champignon(s), Ei(er).</summary>
    private static readonly string[] PluralEndings = ["e", "en", "er", "n", "s"];

    /// <summary>A word after which the rest says what it is for, which is preparation: "Butter für die Form".</summary>
    private static readonly HashSet<string> Purpose = ["zum", "zur", "fuer", "for"];

    private sealed record Entry(string Key, IngredientWordKind Kind, string[] Forms);

    private static readonly Entry[] Entries =
    [
        Qualifier("green", [.. Declined("gruen"), "green"]),
        Qualifier("white", [.. Declined("weiss"), "white"]),
        Qualifier("red", [.. Declined("rot"), "red"]),
        Qualifier("yellow", [.. Declined("gelb"), "yellow"]),
        Qualifier("black", [.. Declined("schwarz"), "black"]),
        Qualifier("purple", [.. Declined("violett"), "lila", "purple"]),
        Qualifier("brown", [.. Declined("braun"), "brown"]),
        Qualifier("light", ["light", "lite"]),
        Qualifier("reduced_fat",
            [.. Declined("fettarm", "fettreduziert", "mager", "halbfett"), "low fat", "lowfat", "reduced fat", "skimmed", "skim"]),
        Qualifier("full_fat", [.. Declined("vollfett"), "full fat"]),
        Qualifier("unsweetened", [.. Declined("zuckerfrei", "ungesuesst"), "sugar free", "unsweetened"]),
        Qualifier("sweetened", [.. Declined("gesuesst"), "sweetened"]),
        Qualifier("lactose_free", [.. Declined("laktosefrei"), "lactose free"]),
        Qualifier("salted", [.. Declined("gesalzen"), "salted"]),
        Qualifier("unsalted", [.. Declined("ungesalzen"), "unsalted"]),
        Qualifier("wholegrain", ["vollkorn", "whole wheat", "whole grain", "wholegrain", "wholemeal"]),
        Qualifier("frozen", ["tk", .. Declined("tiefgekuehlt", "tiefgefroren", "gefroren"), "frozen"]),
        Qualifier("dried", [.. Declined("getrocknet"), "dried"]),
        Qualifier("smoked", [.. Declined("geraeuchert"), "smoked"]),
        Qualifier("canned", ["aus der dose", "dose", "dosen", "konserve", "canned", "tinned"]),
        Qualifier("ground", [.. Declined("gemahlen"), "ground"]),
        Qualifier("whole", [.. Declined("ganz"), "whole"]),
        Qualifier("raw", [.. Declined("roh"), "raw"]),
        Qualifier("cooked", [.. Declined("gekocht"), "cooked"]),
        Qualifier("sweet", [.. Declined("suess"), "sweet"]),
        Qualifier("hot", [.. Declined("scharf"), "spicy", "hot"]),
        Qualifier("mild", [.. Declined("mild")]),
        Qualifier("dry", [.. Declined("trocken")]),

        Descriptor("large", [.. Declined("gross", "riesig"), "large", "big", "jumbo"]),
        Descriptor("small", [.. Declined("klein"), "small"]),
        Descriptor("medium", [.. Declined("mittelgross", "mittler"), "medium"]),
        Descriptor("organic", ["bio", "organic"]),
        Descriptor("fresh", [.. Declined("frisch"), "fresh"]),
        Descriptor("ripe", [.. Declined("reif"), "ripe"]),
        Descriptor("good", [.. Declined("gut"), "good"]),

        Preparation(
        [
            .. Declined(
                "gehackt", "feingehackt", "gewuerfelt", "geschnitten", "kleingeschnitten", "geschaelt", "gerieben",
                "geraspelt", "zerkleinert", "gepresst", "zerdrueckt", "gehobelt", "halbiert", "geviertelt", "entkernt",
                "gewaschen", "geputzt", "abgetropft", "abgespuelt", "zimmerwarm", "weich", "kalt", "warm",
                "geschmolzen", "zerlassen", "gebraten", "geroestet", "blanchiert", "passiert", "gestiftelt",
                "zerpflueckt"),
            "fein", "grob", "in scheiben", "in wuerfeln", "in streifen", "in ringen", "nach belieben",
            "nach geschmack", "optional",
            "chopped", "diced", "sliced", "minced", "grated", "peeled", "crushed", "softened", "melted", "halved",
            "quartered", "drained", "rinsed", "cubed", "shredded", "finely", "roughly", "room temperature",
            "to taste"
        ])
    ];

    /// <summary>The qualifier forms that also begin a compound: "Grünspargel" is green asparagus.</summary>
    private static readonly (string Prefix, string Key)[] CompoundPrefixes =
    [
        ("gruen", "green"), ("weiss", "white"), ("rot", "red"), ("gelb", "yellow"), ("schwarz", "black"),
        ("vollkorn", "wholegrain"), ("tiefkuehl", "frozen")
    ];

    private static readonly Dictionary<string, Entry> Phrases = Index();

    /// <summary>Reads a name.</summary>
    /// <param name="name">An ingredient name as written: "Spargel, grün".</param>
    public static IngredientReading Read(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var words = Words(name);

        return new IngredientReading(
            [.. words.Where(word => word.Kind is null).Select(word => word.Text)],
            Keys(words, IngredientWordKind.Qualifier),
            Keys(words, IngredientWordKind.Descriptor),
            [.. words.Where(word => word.Kind == IngredientWordKind.Preparation).Select(word => word.Text)]);
    }

    /// <summary>
    /// Reads a name back in the words it was written in, for the cook to check: "fein gehackte rote Zwiebeln" is
    /// understood as "Zwiebeln, rot", and would be "rote Zwiebeln" with "fein gehackt" in the note.
    /// </summary>
    /// <param name="name">An ingredient name as written.</param>
    public static IngredientWording Wording(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var tokens = Tokens(name);
        var owners = tokens.SelectMany((token, index) => Fold(token.Text).Select(_ => index)).ToArray();
        var words = Words(name).ToLookup(word => owners[word.At]);

        // A written word is the thing when any part of it is: "Grünspargel" is not only green.
        IngredientWordKind? KindOf(int at) => words[at].Any(word => word.Kind is null) ? null : words[at].First().Kind;

        int[] Where(Func<IngredientWordKind?, bool> kind) => [.. Enumerable.Range(0, tokens.Count).Where(at => kind(KindOf(at)))];

        var kinds = Where(kind => kind == IngredientWordKind.Qualifier).Select(at => Plain(tokens, at, words[at])).ToList();
        var understood = Joined(name, tokens, Where(kind => kind is null)) + (kinds.Count > 0 ? ", " + string.Join(' ', kinds) : string.Empty);
        var preparation = Where(kind => kind == IngredientWordKind.Preparation);

        if (preparation.Length == 0)
        {
            return new IngredientWording(understood, null, null);
        }

        // Words apart in the name are apart in the note: "gehackte Zwiebeln, geschält" is "gehackt, geschält".
        var note = string.Concat(preparation.Select((at, index) =>
            (index == 0 ? string.Empty : at == preparation[index - 1] + 1 ? name[tokens[at - 1].End..tokens[at].Start] : ", ")
            + Plain(tokens, at, words[at])));

        return new IngredientWording(understood, Joined(name, tokens, Where(kind => kind != IngredientWordKind.Preparation)), note);
    }

    /// <summary>
    /// The other ways a name is written, for a table that lists only one, folded: "große Zwiebeln" is also
    /// "zwiebeln", "Spargel, grün" also "gruener spargel" and "spargel gruen". The name as written is not among them.
    /// </summary>
    /// <param name="name">An ingredient name as written.</param>
    public static IEnumerable<string> Spellings(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var words = Words(name);
        var plain = string.Join(' ', words.Where(word => !IsAbout(word)).Select(word => word.Text));

        if (words.Any(IsAbout))
        {
            yield return plain;
        }

        var kinds = Keys(words, IngredientWordKind.Qualifier);

        if (kinds.Length != 1)
        {
            yield break;
        }

        // One kind, in each place and ending German writes it: "grüner Spargel", "Spargel grün".
        var thing = string.Join(' ', words.Where(word => word.Kind is null).Select(word => word.Text));

        foreach (var form in Entries.First(entry => entry.Key == kinds[0]).Forms.Where(form => !form.Contains(' ')))
        {
            yield return $"{form} {thing}";
            yield return $"{thing} {form}";
        }
    }

    private static List<Word> Words(string name)
    {
        var tokens = Tokens(name);
        var nouns = WrittenAsNouns(tokens);
        var words = Classify([.. tokens.SelectMany(token => Fold(token.Text))]);

        // German capitalises a noun: in "gemischtes Gehacktes" the participle is the thing, not its preparation. Not
        // a noun inside a phrase: "nach Belieben" is still preparation.
        words = [.. words.Select(word => IsAbout(word) && word.Key is not null && Phrases.ContainsKey(word.Text) && nouns.Contains(word.Text)
            ? word with { Kind = null, Key = null }
            : word)];

        // "gehackte Tomaten" is a can, not chopped tomatoes: the words go back to the thing when the whole name is a
        // food the rest is not.
        if (words.Any(IsAbout) &&
            FoodNames.Match(name)?.Code is { } food &&
            FoodNames.Match(string.Join(' ', words.Where(word => !IsAbout(word)).Select(word => word.Text)))?.Code != food)
        {
            words = [.. words.Select(word => IsAbout(word) ? word with { Kind = null, Key = null } : word)];
        }

        // A name that is nothing but words about a thing ("Bio", "Gehacktes") is itself the thing.
        if (words.All(word => word.Kind is not null))
        {
            words = [.. words.Select(word => word with { Kind = null, Key = null })];
        }

        return words;
    }

    /// <summary>
    /// Whether two folded words are one, give or take a plural ending or in a listed plural: "tomate" and "tomaten",
    /// "apfel" and "aepfel", not "butt" and "butter".
    /// </summary>
    /// <param name="one">A folded word.</param>
    /// <param name="other">Another.</param>
    public static bool AreOneWord(string one, string other)
    {
        ArgumentNullException.ThrowIfNull(one);
        ArgumentNullException.ThrowIfNull(other);

        return one == other || EndsInPlural(one, other) || EndsInPlural(other, one) || IngredientNouns.AreOneNoun(one, other);
    }

    /// <summary>Whether every word of <paramref name="tail"/> qualifies a thing: "grün", "light", "aus der Dose".</summary>
    /// <param name="tail">Text written after a comma.</param>
    public static bool AreAllQualifiers(string tail)
    {
        ArgumentNullException.ThrowIfNull(tail);

        var words = Classify(Fold(tail));

        return words.Count > 0 && words.All(word => word.Kind == IngredientWordKind.Qualifier);
    }

    /// <summary>The words written with a capital after the first, folded: a capital opening a name says nothing.</summary>
    private static HashSet<string> WrittenAsNouns(List<Written> tokens) =>
        [.. tokens.Skip(1).Where(token => char.IsUpper(token.Text[0])).SelectMany(token => Fold(token.Text))];

    /// <summary>A word as written, and where: the letters and digits between anything else.</summary>
    private sealed record Written(string Text, int Start)
    {
        public int End => Start + Text.Length;
    }

    private static List<Written> Tokens(string name)
    {
        var tokens = new List<Written>();

        for (var at = 0; at < name.Length; at++)
        {
            var start = at;

            while (at < name.Length && char.IsLetterOrDigit(name[at]))
            {
                at++;
            }

            if (at > start)
            {
                tokens.Add(new Written(name[start..at], start));
            }
        }

        return tokens;
    }

    private static string[] Fold(string text) => SearchText.FoldAe(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The written words at <paramref name="indices"/>, with what was written before each but the first.</summary>
    private static string Joined(string name, List<Written> tokens, int[] indices) =>
        string.Concat(indices.Select((at, index) => (index == 0 ? string.Empty : name[tokens[at - 1].End..tokens[at].Start]) + tokens[at].Text));

    /// <summary>
    /// A written word in its plain form, the way it reads on its own: "gehackte" is "gehackt", "Rote" opening the
    /// name is "rot". A word outside the vocabulary is left as written.
    /// </summary>
    private static string Plain(List<Written> tokens, int at, IEnumerable<Word> words)
    {
        var text = tokens[at].Text;

        if (words.ToList() is not [{ Key: not null } word])
        {
            return text;
        }

        var ending = Declensions.FirstOrDefault(ending =>
            ending.Length > 0 &&
            word.Text.EndsWith(ending, StringComparison.Ordinal) &&
            Phrases.TryGetValue(word.Text[..^ending.Length], out var stem) &&
            stem == Phrases.GetValueOrDefault(word.Text)) ?? string.Empty;

        text = text[..^ending.Length];

        // Only the opening capital is the sentence's: "TK" stays, and so does "Dose" in "aus der Dose".
        return at == 0 && text.Length > 1 && char.IsUpper(text[0]) && char.IsLower(text[1])
            ? char.ToLowerInvariant(text[0]) + text[1..]
            : text;
    }

    /// <summary>A folded word, what it does, and <see cref="At"/>: its place among the folded words of the name.</summary>
    private sealed record Word(string Text, IngredientWordKind? Kind, string? Key, int At, bool Continues = false);

    private static List<Word> Classify(string[] folded)
    {
        var words = new List<Word>();

        for (var at = 0; at < folded.Length; at++)
        {
            if (Purpose.Contains(folded[at]))
            {
                words.AddRange(folded[at..].Select((word, index) => new Word(word, IngredientWordKind.Preparation, null, at + index)));

                break;
            }

            if (PhraseAt(folded, at) is var (entry, length) && entry is not null)
            {
                words.AddRange(folded[at..(at + length)].Select((word, index) => new Word(word, entry.Kind, entry.Key, at + index, index > 0)));
                at += length - 1;

                continue;
            }

            words.AddRange(Compound(folded[at], at));
        }

        return words;
    }

    private static (Entry? Entry, int Length) PhraseAt(string[] words, int at)
    {
        for (var length = Math.Min(LongestPhrase, words.Length - at); length > 0; length--)
        {
            if (Phrases.TryGetValue(string.Join(' ', words[at..(at + length)]), out var entry))
            {
                return (entry, length);
            }
        }

        return (null, 0);
    }

    // Split only where the rest is a food on its own: "gruenspargel", not "rotisserie" or "schwarzwaelder".
    private static IEnumerable<Word> Compound(string word, int at)
    {
        foreach (var (prefix, key) in CompoundPrefixes)
        {
            var rest = word.Length > prefix.Length ? word[prefix.Length..] : string.Empty;

            if (word.StartsWith(prefix, StringComparison.Ordinal) && rest.Length >= ShortestCompoundRest && FoodNames.Match(rest) is not null)
            {
                return [new Word(prefix, IngredientWordKind.Qualifier, key, at), new Word(rest, null, null, at)];
            }
        }

        return [new Word(word, null, null, at)];
    }

    private static bool IsAbout(Word word) => word.Kind is IngredientWordKind.Descriptor or IngredientWordKind.Preparation;

    private static string[] Keys(IEnumerable<Word> words, IngredientWordKind kind) =>
        [.. words.Where(word => word.Kind == kind && !word.Continues).Select(word => word.Key!).Distinct(StringComparer.Ordinal)];

    private static bool EndsInPlural(string longer, string shorter) =>
        shorter.Length >= ShortestStem &&
        longer.StartsWith(shorter, StringComparison.Ordinal) &&
        PluralEndings.Contains(longer[shorter.Length..]);

    private static IEnumerable<string> Declined(params string[] stems) =>
        stems.SelectMany(stem => Declensions.Select(ending => stem + ending));

    private static Entry Qualifier(string key, string[] forms) => new(key, IngredientWordKind.Qualifier, forms);

    private static Entry Descriptor(string key, string[] forms) => new(key, IngredientWordKind.Descriptor, forms);

    private static Entry Preparation(string[] forms) => new("preparation", IngredientWordKind.Preparation, forms);

    private static Dictionary<string, Entry> Index()
    {
        var index = new Dictionary<string, Entry>(StringComparer.Ordinal);

        foreach (var entry in Entries)
        {
            foreach (var form in entry.Forms)
            {
                index.Add(form, entry);
            }
        }

        return index;
    }
}
