using System.Text;

namespace Domain.Search;

/// <summary>
/// The forms two pieces of text are compared in, in both transliterations German is written with.
/// </summary>
/// <remarks>
/// "Müsli", "Muesli" and "Musli" are one word to a person: <c>ItemName.Fold</c> uses ü → ue,
/// PostgreSQL's <c>unaccent</c> ü → u, so both are produced. They match the database's
/// <c>culina_fold_ae</c> and <c>culina_fold_a</c> (migration 0012) exactly; an integration test
/// holds them to it.
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
    /// What each compound word of a query is about when its head says nothing: "Sommergericht" is
    /// about <em>sommer</em>. Folded the ä → ae way.
    /// </summary>
    /// <remarks>
    /// A compound is a kind of its last part (a Fischsuppe is a soup), so its first part reads on
    /// its own only when the head adds nothing (Gericht, Essen, Rezept). A modifier ending in s
    /// gives two readings (Sonntag·s·essen, Mais·gericht); whichever is not a tag matches nothing.
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

    /// <summary>
    /// The heads of a compound that say nothing, each plural before its singular.
    /// </summary>
    private static readonly string[] EmptyHeads =
        ["gerichte", "gericht", "essen", "rezepte", "rezept", "kueche", "ideen", "idee", "speisen", "speise"];

    /// <summary>Four letters, as short as a tag worth finding this way can be: "Ofen".</summary>
    private const int ShortestModifier = 4;

    private static char Translate(char character)
    {
        var at = Accented.IndexOf(character, StringComparison.Ordinal);

        return at < 0 ? character : Plain[at];
    }

    /// <summary>Lower-cases ASCII and turns every run of anything else into one space.</summary>
    /// <remarks>
    /// ASCII only, like the database (<c>--locale=C</c>, where <c>lower()</c> leaves unknown
    /// capitals alone); it also keeps <c>%</c> and <c>_</c> out of every LIKE pattern built from a
    /// fold.
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
