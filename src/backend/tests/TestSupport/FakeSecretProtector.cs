using Application.Abstractions;

namespace TestSupport;

/// <summary>Wraps rather than encrypts, so tests can assert a value is stored protected, and can simulate lost keys with a flag.</summary>
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
