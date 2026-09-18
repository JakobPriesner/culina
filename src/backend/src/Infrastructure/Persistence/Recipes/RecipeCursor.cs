using System.Globalization;
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
    internal string Encode() => PageCursor.Encode(this);

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
        var cursor = PageCursor.TryDecode<RecipeCursor>(encoded);

        return cursor?.Sort == sort ? cursor : null;
    }

    internal static string Key(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    internal static string Key(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    internal static string Key(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A relevance score, round-tripped exactly.
    /// </summary>
    /// <remarks>
    /// "R" rather than a fixed number of decimal places: a cursor that rounds
    /// its own sort key is a cursor that can skip the row after it, or return
    /// that row twice, and either one looks like a paging bug nobody can
    /// reproduce.
    /// </remarks>
    internal static string Key(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
