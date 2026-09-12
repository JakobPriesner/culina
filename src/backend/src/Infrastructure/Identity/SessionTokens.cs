using System.Security.Cryptography;
using System.Text;
using Application.Abstractions;

namespace Infrastructure.Identity;

/// <summary>
/// 256-bit random tokens, stored as SHA-256 digests.
/// </summary>
/// <remarks>
/// <para>
/// A plain hash rather than a password hash: the token is already full-entropy
/// random, so there is nothing to brute-force and no reason to pay Argon2's
/// cost on every single request.
/// </para>
/// <para>
/// Storing the digest rather than the token means a database dump cannot be
/// replayed as a live session.
/// </para>
/// </remarks>
internal sealed class SessionTokens : ISessionTokens
{
    private const int TokenBytes = 32;

    public string NewToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

    public ReadOnlyMemory<byte> Digest(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public bool Matches(string token, ReadOnlyMemory<byte> digest) =>
        CryptographicOperations.FixedTimeEquals(Digest(token).Span, digest.Span);

    /// <summary>
    /// URL-safe and unpadded, because the value travels in a cookie where
    /// <c>+</c>, <c>/</c> and <c>=</c> all need escaping.
    /// </summary>
    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
