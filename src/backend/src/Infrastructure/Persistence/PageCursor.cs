using System.Buffers.Text;
using System.Text.Json;

namespace Infrastructure.Persistence;

/// <summary>
/// Reads and writes the opaque token that says where a page ended.
/// </summary>
/// <remarks>
/// <para>
/// Every keyset cursor in the application is the same thing underneath — a
/// small record, as JSON, as URL-safe unpadded base64 — and only the record
/// differs. Written out per cursor, the encoding drifted apart from itself:
/// the padding arithmetic alone existed three times. A cursor is part of the
/// API contract, so one copy fixed and another missed would have broken one
/// endpoint's paging and left the other working.
/// </para>
/// <para>
/// <see cref="Base64Url"/> rather than a fourth hand-rolled pad-and-translate.
/// It produces the same text the hand-rolled version did, so tokens already
/// issued keep working.
/// </para>
/// </remarks>
internal static class PageCursor
{
    /// <summary>Writes a cursor as a token safe to put in a query string.</summary>
    /// <typeparam name="T">The cursor record.</typeparam>
    /// <param name="cursor">What to write.</param>
    internal static string Encode<T>(T cursor) =>
        Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(cursor));

    /// <summary>
    /// Reads a cursor, or null when it is absent or unreadable.
    /// </summary>
    /// <typeparam name="T">The cursor record.</typeparam>
    /// <param name="encoded">What the caller sent back.</param>
    /// <remarks>
    /// An unreadable cursor gives the first page rather than an error. It is
    /// opaque, so a caller cannot have meant anything by a malformed one, and
    /// the first page is the only answer that is never wrong.
    /// </remarks>
    internal static T? TryDecode<T>(string? encoded)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(Base64Url.DecodeFromChars(encoded));
        }
        catch (Exception failure) when (failure is FormatException or JsonException)
        {
            return null;
        }
    }
}
