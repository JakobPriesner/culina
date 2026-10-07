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
    /// <para>
    /// A cursor that does not match the current sort is ignored rather than
    /// rejected: the caller changed the ordering, which means they want the
    /// first page of the new order, not an error.
    /// </para>
    /// <para>
    /// So is one whose keys are not what its sort writes. A cursor is opaque
    /// but not sealed — anybody can base64 some JSON — and the keys are cast
    /// in SQL, so a word where a count belongs, a key too few or a null would
    /// otherwise fail inside PostgreSQL and come back as a 500. Each key is
    /// read as its type and written back through the same <c>Key</c> overload
    /// a real cursor was written with, so the SQL only ever sees text this
    /// class produced.
    /// </para>
    /// </remarks>
    internal static RecipeCursor? Decode(string? encoded, RecipeSort sort)
    {
        var cursor = PageCursor.TryDecode<RecipeCursor>(encoded);

        if (cursor?.Sort != sort || cursor.Keys is null)
        {
            return null;
        }

        var readers = KeyReaders(sort);

        if (cursor.Keys.Count != readers.Length)
        {
            return null;
        }

        var keys = cursor.Keys.Zip(readers, (key, read) => key is null ? null : read(key)).ToList();

        return keys.Contains(null) ? null : cursor with { Keys = keys! };
    }

    /// <summary>
    /// How each key of a sort is read back, in the order
    /// <see cref="RecipeSearchSql.KeysOf"/> writes them. A reader answers null
    /// for a key that is not its type.
    /// </summary>
    /// <remarks>
    /// The counts — a tier, minutes, times cooked — are never negative, so a
    /// sign is refused along with everything else that is not a digit. That
    /// also keeps the tier clear of the one int PostgreSQL cannot negate.
    /// </remarks>
    private static Func<string, string?>[] KeyReaders(RecipeSort sort) => sort switch
    {
        RecipeSort.Title => [title => title],
        RecipeSort.ShortestFirst => [minutes => minutes.Length == 0 ? minutes : Count(minutes)],
        RecipeSort.MostCooked => [Count],
        RecipeSort.Relevance => [Count, Score, Time],
        RecipeSort.Suggested => [SuggestionScore],
        _ => [Time]
    };

    private static string? Count(string key) =>
        int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? Key(value)
            : null;

    private static string? Score(string key) =>
        double.TryParse(key, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
        && double.IsFinite(value)
            ? Key(value)
            : null;

    private static string? SuggestionScore(string key) =>
        decimal.TryParse(key, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Key(value)
            : null;

    private static string? Time(string key) =>
        DateTimeOffset.TryParse(key, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? Key(value)
            : null;

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

    /// <summary>
    /// A suggestion score, round-tripped exactly.
    /// </summary>
    /// <remarks>
    /// The same rule one type along. "G" is decimal's round-trip format, and
    /// the scoring already rounds to six places before this sees one — so the
    /// cursor carries the number the order was cut on rather than a number near
    /// it.
    /// </remarks>
    internal static string Key(decimal value) => value.ToString("G", CultureInfo.InvariantCulture);
}
