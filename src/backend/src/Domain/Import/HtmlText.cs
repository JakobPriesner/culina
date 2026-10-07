using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Domain.Import;

/// <summary>Pulls the two things an import needs out of a page's markup.</summary>
/// <remarks>
/// Regular expressions on purpose: a parser is a dependency and attack surface, and the output is
/// shown back for correction.
/// </remarks>
public static partial class HtmlText
{
    /// <summary>Every JSON-LD block a page carries.</summary>
    public static IReadOnlyList<string> JsonLdBlocks(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return [];
        }

        return
        [
            .. ScriptBlocks()
                .Matches(html)
                .Select(match => WebUtility.HtmlDecode(match.Groups["body"].Value).Trim())
                .Where(block => block.Length > 0)
        ];
    }

    /// <summary>The words of a page, as somebody reading it would see them.</summary>
    /// <remarks>
    /// The fallback for sites without structured data; the client reads it with the same parser as
    /// a pasted recipe.
    /// </remarks>
    public static string ReadableText(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        // Script and style first: their contents are text to a regex and gibberish to a reader.
        var stripped = NonContent().Replace(html, " ");

        // Block elements become line breaks: the paste parser reads lines.
        stripped = LineBreaks().Replace(stripped, "\n");
        stripped = Tags().Replace(stripped, " ");

        var text = WebUtility.HtmlDecode(stripped);
        var lines = new StringBuilder();

        foreach (var line in text.Split('\n'))
        {
            var collapsed = string.Join(
                ' ',
                line.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            if (collapsed.Length > 0)
            {
                lines.Append(collapsed).Append('\n');
            }
        }

        return lines.ToString();
    }

    [GeneratedRegex(
        """<script\b[^>]*\btype\s*=\s*["']application/ld\+json["'][^>]*>(?<body>.*?)</script\s*>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex ScriptBlocks();

    [GeneratedRegex(
        """<(script|style|noscript|svg|template)\b[^>]*>.*?</\1\s*>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex NonContent();

    [GeneratedRegex(
        """</?(p|div|li|ul|ol|br|h[1-6]|tr|section|article|header|footer)\b[^>]*>""",
        RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex LineBreaks();

    [GeneratedRegex("<[^>]*>", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Tags();
}
