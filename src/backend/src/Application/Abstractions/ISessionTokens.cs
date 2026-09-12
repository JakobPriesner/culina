namespace Application.Abstractions;

/// <summary>
/// Creates the two secrets a session needs, and hashes them for storage.
/// </summary>
/// <remarks>
/// A port because the randomness is a technology concern and a test must be
/// able to pin what it produces. The session cookie value and the CSRF token
/// are separate secrets on purpose: the cookie is unreadable to script and the
/// CSRF token must be readable, so one cannot stand in for the other.
/// </remarks>
public interface ISessionTokens
{
    /// <summary>A fresh secret, safe to put in a cookie.</summary>
    string NewToken();

    /// <summary>The digest stored beside the session.</summary>
    /// <param name="token">The raw token.</param>
    ReadOnlyMemory<byte> Digest(string token);

    /// <summary>Compares a presented token with a stored digest in constant time.</summary>
    /// <param name="token">What the caller presented.</param>
    /// <param name="digest">What was stored.</param>
    bool Matches(string token, ReadOnlyMemory<byte> digest);
}
