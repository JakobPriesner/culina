namespace Infrastructure.Persistence.Recipes;

/// <summary>Where the previous page of related recipes ended: keyset on the score, then timestamp and id as tiebreakers.</summary>
/// <param name="Score">The last row's score, already rounded to six places.</param>
/// <param name="UpdatedAt">The last row's timestamp.</param>
/// <param name="Id">The last row's id.</param>
internal sealed record RelatedCursor(decimal Score, DateTimeOffset UpdatedAt, Guid Id)
{
    internal string Encode() => PageCursor.Encode(this);

    /// <summary>Reads a cursor, or null when absent or unreadable. The time is put back into UTC, as <see cref="Cookbooks.CookbookCursor.Decode"/> explains.</summary>
    /// <param name="encoded">What the caller sent back.</param>
    internal static RelatedCursor? Decode(string? encoded) =>
        PageCursor.TryDecode<RelatedCursor>(encoded) is { } cursor
            ? cursor with { UpdatedAt = cursor.UpdatedAt.ToUniversalTime() }
            : null;
}
