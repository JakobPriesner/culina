using System.Globalization;
using System.Text;
using System.Text.Json;
using Application.Abstractions;

namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// Where the previous page ended.
/// </summary>
/// <remarks>
/// <para>
/// Keyset paging, not offsets: a recipe inserted while someone is scrolling
/// would otherwise shift every later page by one and make a row appear twice
/// or not at all. The cursor carries the sort key of the last row plus its id,
/// so the next page starts exactly after it regardless of what changed.
/// </para>
/// <para>
/// The id tiebreaker matters even for sorts that look unique: two recipes saved
/// in the same millisecond, or two with the same title, would otherwise have no
/// stable order and the same row could be returned on two pages.
/// </para>
/// </remarks>
/// <param name="Sort">The order this cursor belongs to.</param>
/// <param name="Keys">The last row's sort key values, in order.</param>
/// <param name="Id">The last row's id.</param>
internal sealed record RecipeCursor(RecipeSort Sort, IReadOnlyList<string> Keys, Guid Id)
{
    internal string Encode() =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>
    /// Reads a cursor, or null when it is absent, unreadable, or from a
    /// different sort order.
    /// </summary>
    /// <remarks>
    /// A cursor that does not match the current sort is ignored rather than
    /// rejected: the caller changed the ordering, which means they want the
    /// first page of the new order, not an error.
    /// </remarks>
    internal static RecipeCursor? Decode(string? encoded, RecipeSort sort)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        try
        {
            var padded = encoded.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');

            var cursor = JsonSerializer.Deserialize<RecipeCursor>(
                Encoding.UTF8.GetString(Convert.FromBase64String(padded)));

            return cursor?.Sort == sort ? cursor : null;
        }
        catch (Exception failure) when (failure is FormatException or JsonException)
        {
            return null;
        }
    }

    internal static string Key(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    internal static string Key(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    internal static string Key(int value) => value.ToString(CultureInfo.InvariantCulture);
}
