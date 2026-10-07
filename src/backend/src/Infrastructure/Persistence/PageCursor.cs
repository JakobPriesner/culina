using System.Buffers.Text;
using System.Text.Json;

namespace Infrastructure.Persistence;

/// <summary>Reads and writes the opaque token that says where a page ended: a small record as JSON in URL-safe unpadded base64.</summary>
/// <remarks>One encoding for every keyset cursor, since a cursor is API contract and per-cursor copies drifted. Output matches the old hand-rolled version, so issued tokens still work.</remarks>
internal static class PageCursor
{
    /// <summary>Writes a cursor as a token safe to put in a query string.</summary>
    internal static string Encode<T>(T cursor) =>
        Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(cursor));

    /// <summary>Reads a cursor, or null when absent or unreadable; an unreadable one yields the first page, the only never-wrong answer.</summary>
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
