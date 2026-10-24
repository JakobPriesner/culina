using System.Globalization;
using System.Text;

namespace Domain.AgentAuthorizations;

/// <summary>An app that connects AI agents, as the consent screen names it.</summary>
/// <param name="ClientId">
/// Its OAuth <c>client_id</c>: the https address of its Client ID Metadata Document, or the id Culina issued when it
/// registered itself.
/// </param>
/// <param name="Name">What it calls itself, cleaned for showing as text.</param>
/// <param name="VerifiedHost">
/// The host its metadata document was read from, which vouches for the name; null for a self-registered app, whose
/// name nothing vouches for.
/// </param>
public sealed record AgentClient(string ClientId, string Name, string? VerifiedHost)
{
    private const int MaxNameLength = 80;

    /// <summary>
    /// The app's own name, made safe to show: no control or formatting characters (a right-to-left override can make a
    /// name read as another), single spaces, and short. <paramref name="fallback"/> when nothing is left.
    /// </summary>
    public static string CleanName(string? name, string fallback)
    {
        var cleaned = new StringBuilder();

        foreach (var character in name ?? string.Empty)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator
                or UnicodeCategory.ParagraphSeparator or UnicodeCategory.SpaceSeparator)
            {
                cleaned.Append(' ');
            }
            else
            {
                cleaned.Append(character);
            }
        }

        var words = cleaned.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var joined = string.Join(' ', words);

        if (joined.Length > MaxNameLength)
        {
            // Never half of a character written as a surrogate pair.
            var length = char.IsHighSurrogate(joined[MaxNameLength - 1]) ? MaxNameLength - 1 : MaxNameLength;

            joined = joined[..length].TrimEnd();
        }

        return joined.Length == 0 ? fallback : joined;
    }
}

/// <summary>An app and the return addresses it registered, which an authorization request must name one of.</summary>
/// <param name="Client">The app.</param>
/// <param name="RedirectUris">Its registered return addresses.</param>
public sealed record ClientRegistration(AgentClient Client, IReadOnlyList<string> RedirectUris)
{
    /// <summary>Whether the app may be sent back to this address.</summary>
    public bool Accepts(string redirectUri) =>
        RedirectAddress.IsAcceptable(redirectUri)
        && RedirectUris.Any(registered => RedirectAddress.IsAcceptable(registered)
            && RedirectAddress.Matches(registered, redirectUri));
}
