using System.Globalization;
using Application.Abstractions;

namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// Where the previous page ended. Keyset paging, so a concurrent insert cannot shift later pages;
/// the id tiebreaker keeps rows with equal sort keys in a stable order.
/// </summary>
/// <param name="Sort">The order this cursor belongs to.</param>
/// <param name="Keys">The last row's sort key values, in order.</param>
/// <param name="Id">The last row's id.</param>
internal sealed record RecipeCursor(RecipeSort Sort, IReadOnlyList<string> Keys, Guid Id)
{
    internal string Encode() => PageCursor.Encode(this);

    /// <summary>Reads a cursor, or null when absent, unreadable, from a different sort, or with keys its sort would not write.</summary>
    /// <remarks>
    /// Ignored, not rejected: a changed ordering wants page one. Keys are cast in SQL, so a forged cursor
    /// would 500 in PostgreSQL; each is re-read as its type and rewritten through <c>Key</c>.
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

    // How each key of a sort is read back, in the order RecipeSearchSql.KeysOf writes them; null for a key of the wrong type.
    // Counts are never negative, so a sign is refused (which also avoids the one int PostgreSQL cannot negate).
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

    /// <summary>A relevance score, round-tripped exactly with "R": a rounded sort key can skip or repeat the next row.</summary>
    internal static string Key(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>A suggestion score, round-tripped exactly with "G"; scoring already rounds to six places.</summary>
    internal static string Key(decimal value) => value.ToString("G", CultureInfo.InvariantCulture);
}
