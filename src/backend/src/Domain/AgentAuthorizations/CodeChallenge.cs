using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Domain.AgentAuthorizations;

/// <summary>PKCE (RFC 7636) with <c>S256</c>, the only method taken: proves the app redeeming a code is the one that asked for it.</summary>
/// <remarks>
/// <c>plain</c> is refused: it sends the secret itself in the browser's address bar, where the code it guards also
/// travels.
/// </remarks>
public static class CodeChallenge
{
    /// <summary>The one method accepted.</summary>
    public const string Method = "S256";

    // A SHA-256 digest is 32 bytes, 43 characters of unpadded base64url.
    private const int ChallengeLength = 43;

    private const int MinimumVerifierLength = 43;

    private const int MaximumVerifierLength = 128;

    /// <summary>Whether this can be an <c>S256</c> challenge at all.</summary>
    public static bool IsWellFormed(string? challenge) =>
        challenge is { Length: ChallengeLength } && challenge.All(IsBase64UrlCharacter);

    /// <summary>Whether the verifier proves the challenge, compared in constant time.</summary>
    public static bool Verifies(string? verifier, string challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        if (verifier is null
            || verifier.Length is < MinimumVerifierLength or > MaximumVerifierLength
            || !verifier.All(IsUnreserved))
        {
            return false;
        }

        var digest = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var expected = Encoding.ASCII.GetBytes(Base64Url.EncodeToString(digest));

        return CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(challenge));
    }

    private static bool IsBase64UrlCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '_';

    private static bool IsUnreserved(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~';
}
