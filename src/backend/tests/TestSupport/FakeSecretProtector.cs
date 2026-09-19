using Application.Abstractions;

namespace TestSupport;

/// <summary>
/// Stands in for the key ring, visibly.
/// </summary>
/// <remarks>
/// Wraps rather than encrypts, so a test can assert that a stored value is not
/// the plain one without having to decrypt anything — and so the ordinary
/// failure the real one has, a key ring that came back empty, can be arranged
/// with a flag rather than by deleting files.
/// </remarks>
public sealed class FakeSecretProtector : ISecretProtector
{
    private const string Wrapper = "protected:";

    /// <summary>When true, nothing can be read back, as if the keys were lost.</summary>
    public bool KeysLost { get; set; }

    public string Protect(string secret) => Wrapper + secret;

    public string? Unprotect(string protectedSecret)
    {
        if (KeysLost || !protectedSecret.StartsWith(Wrapper, StringComparison.Ordinal))
        {
            return null;
        }

        return protectedSecret[Wrapper.Length..];
    }
}
