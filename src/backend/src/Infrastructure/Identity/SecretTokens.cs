using System.Security.Cryptography;
using System.Text;
using Application.Abstractions;

namespace Infrastructure.Identity;

/// <summary>256-bit random tokens, stored as SHA-256 digests.</summary>
/// <remarks>
/// A plain hash, not a password hash: the token is full-entropy, so Argon2 per request buys
/// nothing. Storing the digest means a database dump cannot be replayed as a session.
/// </remarks>
internal sealed class SecretTokens : ISecretTokens
{
    private const int TokenBytes = 32;

    /// <summary>
    /// Crockford's base 32: no I, L, O or U, so nothing on paper reads as something else.
    /// </summary>
    private const string CodeAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private const int CodeGroups = 4;

    private const int CodeGroupLength = 4;

    public string NewToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

    public string NewRecoveryCode() =>
        string.Join('-', Enumerable.Range(0, CodeGroups).Select(_ =>
            RandomNumberGenerator.GetString(CodeAlphabet, CodeGroupLength)));

    public ReadOnlyMemory<byte> Digest(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public bool Matches(string token, ReadOnlyMemory<byte> digest) =>
        CryptographicOperations.FixedTimeEquals(Digest(token).Span, digest.Span);

    /// <summary>
    /// URL-safe and unpadded, since the value travels in a cookie where <c>+</c>, <c>/</c> and
    /// <c>=</c> need escaping.
    /// </summary>
    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
