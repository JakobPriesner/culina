using Domain.Search;
using Domain.Shared;
using Domain.Shopping;

namespace Domain.Ingredients;

/// <summary>
/// The counted foods in both numbers, so "1 Zwiebel" doubles to "2 Zwiebeln" and "1 Tomate" and "3 Tomaten" are one
/// thing to buy.
/// </summary>
/// <remarks>
/// <para>
/// Listed, not generated: German plurals are not a suffix rule (Apfel, Äpfel; Ei, Eier; Paprika, Paprika), and a
/// generated wrong plural is worse than the word left as written. A word not listed is never changed. A compound
/// takes the forms of its last part, so "Zehe" covers Knoblauchzehe and "Kartoffel" Süßkartoffel.
/// </para>
/// <para>
/// Only counted foods are here: a mass noun (Spargel, Mehl, Kokosmilch) has one form. The frontend shows scaled
/// lines with a copy of this table (<c>features/recipes/nounForms.ts</c>), which a test holds to this one.
/// </para>
/// </remarks>
public static class IngredientNouns
{
    /// <summary>A German noun's gender, which decides the adjective before it in the singular.</summary>
    public enum Gender
    {
        /// <summary>der: "ein großer Apfel".</summary>
        Masculine,

        /// <summary>die: "eine große Zwiebel".</summary>
        Feminine,

        /// <summary>das: "ein kleines Ei".</summary>
        Neuter
    }

    /// <summary>A food in both numbers, as a recipe writes it.</summary>
    /// <param name="One">The singular: "Zwiebel".</param>
    /// <param name="Many">The plural: "Zwiebeln".</param>
    /// <param name="Of">For German, the gender; null for English, whose adjectives do not change.</param>
    public sealed record Noun(string One, string Many, Gender? Of);

    /// <summary>How many words of its own a household may list.</summary>
    public const int MaxOwnPerHousehold = 500;

    /// <summary>The longest word a household may list.</summary>
    private const int LongestOwnForm = 60;

    /// <summary>The shortest a listed form may be and still end a compound: "Zehe" ends Knoblauchzehe, "Ei" ends nothing.</summary>
    private const int ShortestCompoundForm = 4;

    /// <summary>The shortest a compound's front may be: "Brat·äpfel", not "B·rot".</summary>
    private const int ShortestCompoundFront = 3;

    /// <summary>The German table.</summary>
    public static IReadOnlyList<Noun> German { get; } =
    [
        new("Apfel", "Äpfel", Gender.Masculine),
        new("Aprikose", "Aprikosen", Gender.Feminine),
        new("Artischocke", "Artischocken", Gender.Feminine),
        new("Aubergine", "Auberginen", Gender.Feminine),
        new("Avocado", "Avocados", Gender.Feminine),
        new("Banane", "Bananen", Gender.Feminine),
        new("Beere", "Beeren", Gender.Feminine),
        new("Birne", "Birnen", Gender.Feminine),
        new("Blatt", "Blätter", Gender.Neuter),
        new("Brezel", "Brezeln", Gender.Feminine),
        new("Brot", "Brote", Gender.Neuter),
        new("Brust", "Brüste", Gender.Feminine),
        new("Champignon", "Champignons", Gender.Masculine),
        new("Dattel", "Datteln", Gender.Feminine),
        new("Ei", "Eier", Gender.Neuter),
        new("Eigelb", "Eigelbe", Gender.Neuter),
        new("Eiweiß", "Eiweiße", Gender.Neuter),
        new("Feige", "Feigen", Gender.Feminine),
        new("Filet", "Filets", Gender.Neuter),
        new("Frikadelle", "Frikadellen", Gender.Feminine),
        new("Garnele", "Garnelen", Gender.Feminine),
        new("Gurke", "Gurken", Gender.Feminine),
        new("Kapsel", "Kapseln", Gender.Feminine),
        new("Karotte", "Karotten", Gender.Feminine),
        new("Kartoffel", "Kartoffeln", Gender.Feminine),
        new("Keule", "Keulen", Gender.Feminine),
        new("Kirsche", "Kirschen", Gender.Feminine),
        new("Kiwi", "Kiwis", Gender.Feminine),
        new("Kloß", "Klöße", Gender.Masculine),
        new("Knolle", "Knollen", Gender.Feminine),
        new("Limette", "Limetten", Gender.Feminine),
        new("Mandarine", "Mandarinen", Gender.Feminine),
        new("Mandel", "Mandeln", Gender.Feminine),
        new("Mango", "Mangos", Gender.Feminine),
        new("Melone", "Melonen", Gender.Feminine),
        new("Möhre", "Möhren", Gender.Feminine),
        new("Muschel", "Muscheln", Gender.Feminine),
        new("Nektarine", "Nektarinen", Gender.Feminine),
        new("Nelke", "Nelken", Gender.Feminine),
        new("Nuss", "Nüsse", Gender.Feminine),
        new("Olive", "Oliven", Gender.Feminine),
        new("Orange", "Orangen", Gender.Feminine),
        new("Pfirsich", "Pfirsiche", Gender.Masculine),
        new("Pflaume", "Pflaumen", Gender.Feminine),
        new("Pilz", "Pilze", Gender.Masculine),
        new("Rübe", "Rüben", Gender.Feminine),
        new("Sardelle", "Sardellen", Gender.Feminine),
        new("Schalotte", "Schalotten", Gender.Feminine),
        new("Scheibe", "Scheiben", Gender.Feminine),
        new("Schote", "Schoten", Gender.Feminine),
        new("Semmel", "Semmeln", Gender.Feminine),
        new("Spieß", "Spieße", Gender.Masculine),
        new("Stange", "Stangen", Gender.Feminine),
        new("Steak", "Steaks", Gender.Neuter),
        new("Tomate", "Tomaten", Gender.Feminine),
        new("Tortilla", "Tortillas", Gender.Feminine),
        new("Traube", "Trauben", Gender.Feminine),
        new("Wrap", "Wraps", Gender.Masculine),
        new("Wurst", "Würste", Gender.Feminine),
        new("Zehe", "Zehen", Gender.Feminine),
        new("Zitrone", "Zitronen", Gender.Feminine),
        new("Zwiebel", "Zwiebeln", Gender.Feminine)
    ];

    /// <summary>The English table.</summary>
    public static IReadOnlyList<Noun> English { get; } =
    [
        new("apple", "apples", null),
        new("apricot", "apricots", null),
        new("aubergine", "aubergines", null),
        new("avocado", "avocados", null),
        new("banana", "bananas", null),
        new("berry", "berries", null),
        new("breast", "breasts", null),
        new("carrot", "carrots", null),
        new("cherry", "cherries", null),
        new("clove", "cloves", null),
        new("courgette", "courgettes", null),
        new("cucumber", "cucumbers", null),
        new("date", "dates", null),
        new("egg", "eggs", null),
        new("fig", "figs", null),
        new("fillet", "fillets", null),
        new("leaf", "leaves", null),
        new("leek", "leeks", null),
        new("lemon", "lemons", null),
        new("lime", "limes", null),
        new("mango", "mangoes", null),
        new("mushroom", "mushrooms", null),
        new("nectarine", "nectarines", null),
        new("olive", "olives", null),
        new("onion", "onions", null),
        new("orange", "oranges", null),
        new("peach", "peaches", null),
        new("pear", "pears", null),
        new("pepper", "peppers", null),
        new("plum", "plums", null),
        new("potato", "potatoes", null),
        new("prawn", "prawns", null),
        new("radish", "radishes", null),
        new("sausage", "sausages", null),
        new("scallion", "scallions", null),
        new("shallot", "shallots", null),
        new("steak", "steaks", null),
        new("thigh", "thighs", null),
        new("tomato", "tomatoes", null),
        new("tortilla", "tortillas", null)
    ];

    /// <summary>
    /// A household's own food in both numbers, for a word the tables do not list: "Maultasche", "Maultaschen".
    /// Without a gender, so the adjective before it is left as written.
    /// </summary>
    /// <param name="one">The singular.</param>
    /// <param name="many">The plural.</param>
    public static Result<Noun> Own(string? one, string? many)
    {
        var singular = one?.Trim() ?? string.Empty;
        var plural = many?.Trim() ?? string.Empty;

        return IsWord(singular) && IsWord(plural) && ItemName.Fold(singular) != ItemName.Fold(plural)
            ? new Noun(singular, plural, null)
            : IngredientErrors.InvalidNoun;
    }

    /// <summary>Whether <paramref name="noun"/> fits beside the household's <paramref name="existing"/> words; changing the plural of a listed one always does.</summary>
    /// <param name="existing">The words the household already listed.</param>
    /// <param name="noun">The one to list.</param>
    public static Result EnsureRoom(IReadOnlyList<Noun> existing, Noun noun)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(noun);

        return existing.Count >= MaxOwnPerHousehold
               && existing.All(other => ItemName.Fold(other.One) != ItemName.Fold(noun.One))
            ? IngredientErrors.TooManyNouns
            : Result.Success();
    }

    private static bool IsWord(string form) =>
        form.Length is > 0 and <= LongestOwnForm && form.All(character => char.IsLetter(character) || character == '-');

    private static readonly (string One, string Many)[] Folded =
        [.. German.Concat(English).Select(noun => (SearchText.FoldAe(noun.One), SearchText.FoldAe(noun.Many)))];

    /// <summary>
    /// The name with its food in the number the amount asks for: "Zwiebel" for 2 is "Zwiebeln", "große Äpfel" for 1 is
    /// "großer Apfel". Anything else is the name as written.
    /// </summary>
    /// <remarks>
    /// Only a bare count changes the word: after a unit a recipe writes what it means ("500 g Tomaten",
    /// "2 Scheiben Brot"), so that is left alone, as is a line without an amount. Half an onion is "½ Zwiebel".
    /// </remarks>
    /// <param name="name">The name as written.</param>
    /// <param name="amount">How many, or null.</param>
    /// <param name="hasUnit">Whether the amount is in a unit rather than a count.</param>
    /// <param name="language">The recipe's language, or null to try German first, then English.</param>
    /// <param name="own">The household's own nouns (<see cref="Own"/>), looked at with the table.</param>
    public static string ForAmount(
        string name,
        decimal? amount,
        bool hasUnit,
        Language? language,
        IReadOnlyList<Noun>? own = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (amount is null || hasUnit)
        {
            return name;
        }

        var many = amount > 1;
        var words = name.Split(' ');

        // The food is the last word of it that is listed: "Zwiebel, rot" and "rote Zwiebel" alike.
        for (var at = words.Length - 1; at >= 0; at--)
        {
            var (word, trailing) = Trim(words[at]);

            if (Find(word, language, own ?? []) is not { } found)
            {
                continue;
            }

            var (noun, front, isMany) = found;

            if (isMany == many)
            {
                return name;
            }

            words[at] = Respell(word, front, many ? noun.Many : noun.One) + trailing;

            if (noun.Of is { } gender)
            {
                Decline(words, at, gender, many);
            }

            return string.Join(' ', words);
        }

        return name;
    }

    /// <summary>Whether two folded words are one food in two numbers: "apfel" and "aepfel", "knoblauchzehe" and "knoblauchzehen".</summary>
    /// <param name="one">A word folded the ä → ae way.</param>
    /// <param name="other">Another.</param>
    public static bool AreOneNoun(string one, string other)
    {
        ArgumentNullException.ThrowIfNull(one);
        ArgumentNullException.ThrowIfNull(other);

        return Folded.Any(pair => InNumbers(one, other, pair.One, pair.Many) || InNumbers(other, one, pair.One, pair.Many));
    }

    private static bool InNumbers(string singular, string plural, string one, string many) =>
        Ends(singular, one) && Ends(plural, many) &&
        singular[..^one.Length] == plural[..^many.Length];

    private static bool Ends(string word, string form) =>
        word == form || (form.Length >= ShortestCompoundForm && word.Length - form.Length >= ShortestCompoundFront &&
                         word.EndsWith(form, StringComparison.Ordinal));

    private static (Noun Noun, string Front, bool IsMany)? Find(string word, Language? language, IReadOnlyList<Noun> own)
    {
        var table = own.Concat(language switch
        {
            Language.De => German,
            Language.En => English,
            _ => German.Concat(English)
        });

        // Longest form first, so "Knoblauchzehen" is read as Zehen, not as a compound of something shorter.
        return table
            .SelectMany(noun => new[] { (noun, form: noun.Many, isMany: true), (noun, form: noun.One, isMany: false) })
            .OrderByDescending(entry => entry.form.Length)
            .Where(entry => EndsWritten(word, entry.form))
            .Select(entry => ((Noun Noun, string Front, bool IsMany)?)(entry.noun, word[..^entry.form.Length], entry.isMany))
            .FirstOrDefault();
    }

    private static bool EndsWritten(string word, string form) =>
        string.Equals(word, form, StringComparison.OrdinalIgnoreCase) ||
        (form.Length >= ShortestCompoundForm && word.Length - form.Length >= ShortestCompoundFront &&
         word.EndsWith(form, StringComparison.OrdinalIgnoreCase));

    // The listed form in the written word's case: "Zwiebel" stays capitalised, "Knoblauch·zehe" stays lower.
    private static string Respell(string word, string front, string form) =>
        front.Length == 0
            ? (char.IsUpper(word[0]) ? char.ToUpperInvariant(form[0]) : char.ToLowerInvariant(form[0])) + form[1..]
            : front + form.ToLowerInvariant();

    // Only the words right before the food, lower-case as German writes an adjective: "große", "rote".
    private static void Decline(string[] words, int food, Gender gender, bool many)
    {
        for (var at = food - 1; at >= 0 && IsAdjective(words[at]); at--)
        {
            var word = words[at];

            words[at] = many
                ? (word.EndsWith("er", StringComparison.Ordinal) || word.EndsWith("es", StringComparison.Ordinal)
                    ? word[..^1]
                    : word)
                : word.EndsWith('e') ? gender switch
                {
                    Gender.Masculine => word + "r",
                    Gender.Neuter => word + "s",
                    _ => word
                } : word;
        }
    }

    private static readonly HashSet<string> Articles = ["die", "der", "das", "des", "eine", "einer", "eines", "jede", "jeder"];

    private static bool IsAdjective(string word) =>
        word.Length >= 3 && char.IsLower(word[0]) && word.All(char.IsLetter) && !Articles.Contains(word) &&
        (word.EndsWith('e') || word.EndsWith("er", StringComparison.Ordinal) || word.EndsWith("es", StringComparison.Ordinal));

    private static (string Word, string Trailing) Trim(string word)
    {
        var end = word.Length;

        while (end > 0 && !char.IsLetter(word[end - 1]))
        {
            end--;
        }

        return (word[..end], word[end..]);
    }
}
