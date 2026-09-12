namespace Application.Abstractions;

/// <summary>
/// Creates high-entropy secrets and hashes them for storage.
/// </summary>
/// <remarks>
/// <para>
/// One port for every secret Culina hands out — the session cookie value, the
/// CSRF token, an invitation code — because the requirement is identical in all
/// three cases: full-entropy randomness, a digest at rest, and a constant-time
/// comparison. Three copies of that would be three chances to get one wrong.
/// </para>
/// <para>
/// A port because randomness is a technology concern and a test must be able to
/// pin what it produces.
/// </para>
/// </remarks>
public interface ISecretTokens
{
    /// <summary>A fresh secret, safe to put in a cookie or a link.</summary>
    string NewToken();

    /// <summary>The digest stored in place of the secret.</summary>
    /// <param name="token">The raw token.</param>
    ReadOnlyMemory<byte> Digest(string token);

    /// <summary>Compares a presented token with a stored digest in constant time.</summary>
    /// <param name="token">What the caller presented.</param>
    /// <param name="digest">What was stored.</param>
    bool Matches(string token, ReadOnlyMemory<byte> digest);
}
