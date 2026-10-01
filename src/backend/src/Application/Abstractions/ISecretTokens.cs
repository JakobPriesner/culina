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

    /// <summary>
    /// A fresh recovery code: 80 random bits a person can read off paper and
    /// type, as four groups of four characters with no look-alike letters.
    /// </summary>
    /// <remarks>
    /// Shorter than <see cref="NewToken"/> because a person copies it by hand.
    /// Eighty bits is still far beyond what the per-address and per-account
    /// limits let anybody guess, and beyond walking a stolen digest offline.
    /// </remarks>
    string NewRecoveryCode();

    /// <summary>The digest stored in place of the secret.</summary>
    /// <param name="token">The raw token.</param>
    ReadOnlyMemory<byte> Digest(string token);

    /// <summary>Compares a presented token with a stored digest in constant time.</summary>
    /// <param name="token">What the caller presented.</param>
    /// <param name="digest">What was stored.</param>
    bool Matches(string token, ReadOnlyMemory<byte> digest);
}
