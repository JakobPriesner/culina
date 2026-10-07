using System.Globalization;

namespace Infrastructure.Persistence.Cookbooks;

/// <summary>Where the previous page of cookbooks ended.</summary>
/// <remarks>
/// Keyset, so edits mid-scroll do not shift pages; the id breaks ties between cookbooks changed in
/// the same millisecond.
/// </remarks>
/// <param name="UpdatedAt">The last row's timestamp.</param>
/// <param name="Id">The last row's id.</param>
internal sealed record CookbookCursor(DateTimeOffset UpdatedAt, Guid Id)
{
    internal string Encode() => PageCursor.Encode(this);

    /// <summary>Reads a cursor, or null when it is absent or unreadable.</summary>
    /// <remarks>
    /// The time is put back into UTC: Npgsql refuses other offsets, so a hand-made <c>+02:00</c>
    /// cursor was a 500.
    /// </remarks>
    internal static CookbookCursor? Decode(string? encoded) =>
        PageCursor.TryDecode<CookbookCursor>(encoded) is { } cursor
            ? cursor with { UpdatedAt = cursor.UpdatedAt.ToUniversalTime() }
            : null;

    /// <inheritdoc/>
    public override string ToString() =>
        UpdatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
