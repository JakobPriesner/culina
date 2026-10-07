using System.Text;

namespace Domain.Search;

/// <summary>
/// The forms two pieces of text are compared in, in both of the
/// transliterations German is written with.
/// </summary>
/// <remarks>
/// <para>
/// "Müsli", "Muesli" and "Musli" are one word to a person and three to a
/// computer. <c>ItemName.Fold</c> answers this for the shopping list with
/// ü → ue, because that is the spelling a German keyboard falls back to;
/// PostgreSQL's <c>unaccent</c> answers it with ü → u, because that is what
/// stripping a diacritic means. Neither is wrong and they do not meet, so both
/// are produced and both are compared.
/// </para>
/// <para>
/// These are the database's <c>culina_fold_ae</c> and <c>culina_fold_a</c>
/// (migration 0012), character for character, and an integration test holds
/// them to it: the lexicon matches in C# what the lanes match in SQL, and two
/// folds that disagree about one letter are two halves of search that disagree
/// about one word.
/// </para>
/// </remarks>
public static class SearchText
{
    private const string Accented = "ÀÁÂÃÅÈÉÊËÌÍÎÏÒÓÔÕØÙÚÛÑÇÝàáâãåèéêëìíîïòóôõøùúûñçýÿ";
    private const string Plain = "AAAAAEEEEIIIIOOOOOUUUNCYaaaaaeeeeiiiiooooouuuncyy";

    /// <summary>Folds with umlauts expanded: ä becomes ae.</summary>
    public static string FoldAe(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var expanded = new StringBuilder(value.Length + 8);

        foreach (var character in value)
        {
            var replacement = character switch
            {
                'ß' or 'ẞ' => "ss",
                'Ä' => "Ae",
                'ä' => "ae",
                'Ö' => "Oe",
                'ö' => "oe",
                'Ü' => "Ue",
                'ü' => "ue",
                'æ' => "ae",
                _ => null
            };

            // A character is appended as itself, not turned into a string first.
            if (replacement is null)
            {
                expanded.Append(Translate(character));
            }
            else
            {
                expanded.Append(replacement);
            }
        }

        return Words(expanded);
    }

    /// <summary>Folds with the diacritic stripped: ä becomes a.</summary>
    public static string FoldA(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var stripped = new StringBuilder(value.Length + 4);

        foreach (var character in value)
        {
            var replacement = character switch
            {
                'ß' or 'ẞ' => "ss",
                'Ä' => "A",
                'ä' => "a",
                'Ö' => "O",
                'ö' => "o",
                'Ü' => "U",
                'ü' => "u",
                _ => null
            };

            if (replacement is null)
            {
                stripped.Append(Translate(character));
            }
            else
            {
                stripped.Append(replacement);
            }
        }

        return Words(stripped);
    }

    /// <summary>
    /// What each compound word of a query is about, when its head says
    /// nothing: "Sommergericht" is about <em>sommer</em>, "Sonntagsessen" about
    /// <em>sonntag</em>. Folded the ä → ae way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A German compound is a kind of its last part — a Fischsuppe is a soup —
    /// so its first part can only be read on its own when the last part adds
    /// nothing: a Gericht, an Essen, a Rezept is every recipe. That is what lets
    /// a household's own tag answer the word it is buried in, and nothing more:
    /// a tag "Fisch" is no answer to "Fischsuppe".
    /// </para>
    /// <para>
    /// A modifier ending in s gives two readings, because German joins with an
    /// s (Sonntag·s·essen) and some words simply end in one (Mais·gericht).
    /// Whichever is not a tag matches nothing.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> Modifiers(string text)
    {
        var found = new List<string>();

        foreach (var word in FoldAe(text).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var head = EmptyHeads.FirstOrDefault(head =>
                word.EndsWith(head, StringComparison.Ordinal) && word.Length - head.Length >= ShortestModifier);

            if (head is null)
            {
                continue;
            }

            var modifier = word[..^head.Length];
            found.Add(modifier);

            if (modifier.EndsWith('s') && modifier.Length > ShortestModifier)
            {
                found.Add(modifier[..^1]);
            }
        }

        return found;
    }

    /// <summary>The heads of a compound that say nothing, each plural before its singular.</summary>
    private static readonly string[] EmptyHeads =
        ["gerichte", "gericht", "essen", "rezepte", "rezept", "kueche", "ideen", "idee", "speisen", "speise"];

    /// <summary>Four letters, as short as a tag worth finding this way can be: "Ofen".</summary>
    private const int ShortestModifier = 4;

    private static char Translate(char character)
    {
        var at = Accented.IndexOf(character, StringComparison.Ordinal);

        return at < 0 ? character : Plain[at];
    }

    /// <summary>
    /// Lower-cases ASCII and turns every run of anything else into one space.
    /// </summary>
    /// <remarks>
    /// ASCII only, like the database: the cluster runs <c>--locale=C</c>, where
    /// <c>lower()</c> leaves a capital it does not know alone, and whatever is
    /// left that is not a letter or a digit is a separator. That is also what
    /// keeps <c>%</c> and <c>_</c> out of every LIKE pattern built from a fold.
    /// </remarks>
    private static string Words(StringBuilder text)
    {
        var words = new StringBuilder(text.Length);
        var pendingSpace = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            if (character is >= 'A' and <= 'Z')
            {
                character = (char)(character + ('a' - 'A'));
            }

            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                if (pendingSpace && words.Length > 0)
                {
                    words.Append(' ');
                }

                pendingSpace = false;
                words.Append(character);
            }
            else
            {
                pendingSpace = true;
            }
        }

        return words.ToString();
    }
}
