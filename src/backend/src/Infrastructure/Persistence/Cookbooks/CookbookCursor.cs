using System.Globalization;

namespace Infrastructure.Persistence.Cookbooks;

/// <summary>
/// Where the previous page of cookbooks ended.
/// </summary>
/// <remarks>
/// Keyset, like every other page here, and for the same reason: a cookbook
/// touched while somebody is scrolling would shift every later page by one
/// under an offset, and the same shelf would appear twice or not at all. The id
/// is carried alongside the timestamp because two cookbooks changed in the same
/// millisecond otherwise have no stable order.
/// </remarks>
/// <param name="UpdatedAt">The last row's timestamp.</param>
/// <param name="Id">The last row's id.</param>
internal sealed record CookbookCursor(DateTimeOffset UpdatedAt, Guid Id)
{
    internal string Encode() => PageCursor.Encode(this);

    /// <summary>Reads a cursor, or null when it is absent or unreadable.</summary>
    /// <param name="encoded">What the caller sent back.</param>
    /// <remarks>
    /// The time is put back into UTC, which is how every cursor is written.
    /// Npgsql refuses to send a timestamptz with any other offset, so a
    /// hand-made cursor saying <c>+02:00</c> was a 500 rather than the same
    /// instant spelt differently.
    /// </remarks>
    internal static CookbookCursor? Decode(string? encoded) =>
        PageCursor.TryDecode<CookbookCursor>(encoded) is { } cursor
            ? cursor with { UpdatedAt = cursor.UpdatedAt.ToUniversalTime() }
            : null;

    /// <inheritdoc/>
    public override string ToString() =>
        UpdatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
