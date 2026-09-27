namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// Where the previous page of related recipes ended.
/// </summary>
/// <remarks>
/// Keyset, like every other page here: the score the page was cut on, then the
/// timestamp and id that break ties in it, so the next page starts exactly
/// after the last row even when the kitchen has changed in between.
/// </remarks>
/// <param name="Score">The last row's score, already rounded to six places.</param>
/// <param name="UpdatedAt">The last row's timestamp.</param>
/// <param name="Id">The last row's id.</param>
internal sealed record RelatedCursor(decimal Score, DateTimeOffset UpdatedAt, Guid Id)
{
    internal string Encode() => PageCursor.Encode(this);

    /// <summary>Reads a cursor, or null when it is absent or unreadable.</summary>
    /// <param name="encoded">What the caller sent back.</param>
    internal static RelatedCursor? Decode(string? encoded) =>
        PageCursor.TryDecode<RelatedCursor>(encoded);
}
