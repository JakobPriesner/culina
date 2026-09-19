namespace Application.Abstractions;

/// <summary>
/// Encrypts the few values that are stored but must never be readable from a
/// database dump.
/// </summary>
/// <remarks>
/// <para>
/// One caller today: the model provider's API key, which sits in the
/// <c>settings</c> table as part of an otherwise ordinary JSON payload. It is
/// the first thing in Culina that is kept rather than hashed and is worth money
/// to whoever reads it — a session token is hashed because nothing ever needs
/// it back, and a password likewise. A key has to go out on a request, so it
/// has to come back.
/// </para>
/// <para>
/// Backed by the framework's data protection and the key ring at
/// <c>Storage__DataProtectionKeyPath</c>, which has been configured and volume-
/// backed since the beginning and used by nothing. Losing that directory now
/// also means re-entering the API key — which is the mildest of the things
/// losing it was always going to mean.
/// </para>
/// </remarks>
public interface ISecretProtector
{
    /// <summary>Encrypts a secret for storage.</summary>
    /// <param name="secret">The plain value.</param>
    string Protect(string secret);

    /// <summary>
    /// Decrypts a stored secret, or null if it cannot be read.
    /// </summary>
    /// <param name="protectedSecret">What came out of the row.</param>
    /// <remarks>
    /// Null rather than an exception, because there is an ordinary way to get
    /// here: the key ring was lost and restored empty, so the ciphertext is
    /// intact and unreadable. That is "no assistant is configured" and an
    /// administrator re-entering the key, not a crash.
    /// </remarks>
    string? Unprotect(string protectedSecret);
}
