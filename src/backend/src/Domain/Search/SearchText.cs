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
            expanded.Append(character switch
            {
                'ß' or 'ẞ' => "ss",
                'Ä' => "Ae",
                'ä' => "ae",
                'Ö' => "Oe",
                'ö' => "oe",
                'Ü' => "Ue",
                'ü' => "ue",
                'æ' => "ae",
                _ => Translate(character).ToString()
            });
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
            stripped.Append(character switch
            {
                'ß' or 'ẞ' => "ss",
                'Ä' => "A",
                'ä' => "a",
                'Ö' => "O",
                'ö' => "o",
                'Ü' => "U",
                'ü' => "u",
                _ => Translate(character).ToString()
            });
        }

        return Words(stripped);
    }

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
