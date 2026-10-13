using System.Text;
using System.Text.RegularExpressions;

namespace Infrastructure.Import;

/// <summary>Turns a fetched page's bytes into text in the charset the page says it is written in.</summary>
/// <remarks>
/// The order browsers use: a byte-order mark, then the Content-Type charset, then a meta charset near
/// the top, then UTF-8. Like browsers, the Latin-1 and ASCII labels are read as windows-1252, which
/// is what pages labelled so are really written in (a curly quote or a euro sign).
/// </remarks>
internal static partial class PageEncoding
{
    /// <summary>How far into the page a meta charset is looked for; the HTML standard says 1024.</summary>
    private const int MetaWindow = 1024;

    private static readonly HashSet<string> Windows1252 = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows-1252", "cp1252", "x-cp1252", "iso-8859-1", "iso8859-1", "iso_8859-1", "latin1",
        "l1", "us-ascii", "ascii",
    };

    public static string Decode(byte[] bytes, string? declared)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes is [0xEF, 0xBB, 0xBF, ..])
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes is [0xFF, 0xFE, ..])
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes is [0xFE, 0xFF, ..])
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        var encoding = Find(declared) ?? FindInMeta(bytes) ?? Encoding.UTF8;

        return encoding.GetString(bytes);
    }

    private static Encoding? FindInMeta(byte[] bytes)
    {
        // Every charset a meta tag can name agrees with ASCII on the tag itself.
        var top = Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, MetaWindow));
        var match = MetaCharset().Match(top);

        if (!match.Success)
        {
            return null;
        }

        var found = Find(match.Groups["charset"].Value);

        // A page that got this far as ASCII is not UTF-16, whatever it says.
        return found is UnicodeEncoding ? Encoding.UTF8 : found;
    }

    private static Encoding? Find(string? label)
    {
        var name = label?.Trim().Trim('"', '\'');

        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (Windows1252.Contains(name))
        {
            return CodePagesEncodingProvider.Instance.GetEncoding(1252);
        }

        if (CodePagesEncodingProvider.Instance.GetEncoding(name) is { } codePage)
        {
            return codePage;
        }

        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            // A label nobody knows: fall through to the next clue.
            return null;
        }
    }

    /// <summary>Both <c>&lt;meta charset="x"&gt;</c> and the http-equiv form's <c>content="…; charset=x"</c>.</summary>
    [GeneratedRegex("""<meta[^>]*?charset\s*=\s*["']?(?<charset>[\w.:-]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex MetaCharset();
}
