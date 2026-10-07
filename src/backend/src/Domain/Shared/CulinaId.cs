namespace Domain.Shared;

/// <summary>
/// Creates entity identifiers: version 7 UUIDs are time-ordered (append-only index inserts, natural
/// sort) and made by the application.
/// </summary>
public static class CulinaId
{
    /// <summary>A new time-ordered identifier.</summary>
    public static Guid New() => Guid.CreateVersion7();
}
