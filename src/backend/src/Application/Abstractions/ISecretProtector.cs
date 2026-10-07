namespace Application.Abstractions;

/// <summary>Encrypts the few values that are stored but must never be readable from a database dump (provider API keys, source tokens).</summary>
/// <remarks>
/// Encrypted rather than hashed because they must go out on requests. Backed by data protection and the key
/// ring at <c>Storage__DataProtectionKeyPath</c>; losing it means re-entering them.
/// </remarks>
public interface ISecretProtector
{
    /// <summary>Encrypts a secret for storage.</summary>
    string Protect(string secret);

    /// <summary>Decrypts a stored secret, or null if it cannot be read (key ring lost and restored empty), which means "not configured", not a crash.</summary>
    string? Unprotect(string protectedSecret);
}
