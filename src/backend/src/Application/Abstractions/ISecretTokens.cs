namespace Application.Abstractions;

/// <summary>
/// Creates high-entropy secrets and hashes them for storage: one port for the session cookie, CSRF token and invitation code,
/// which all need full-entropy randomness, a digest at rest and constant-time comparison. A port so tests can pin the output.
/// </summary>
public interface ISecretTokens
{
    /// <summary>A fresh secret, safe to put in a cookie or a link.</summary>
    string NewToken();

    /// <summary>A fresh recovery code: 80 random bits as four groups of four characters with no look-alike letters, shorter than a token because people copy it by hand.</summary>
    string NewRecoveryCode();

    /// <summary>The digest stored in place of the secret.</summary>
    /// <param name="token">The raw token.</param>
    ReadOnlyMemory<byte> Digest(string token);

    /// <summary>Compares a presented token with a stored digest in constant time.</summary>
    /// <param name="token">What the caller presented.</param>
    /// <param name="digest">What was stored.</param>
    bool Matches(string token, ReadOnlyMemory<byte> digest);
}
