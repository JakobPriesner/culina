namespace Application.Abstractions;

/// <summary>
/// Hashes and verifies passwords.
/// </summary>
/// <remarks>
/// Asynchronous because a caller may have to wait its turn: each hash holds a
/// large block of memory for its whole run, so only a few run at once and the
/// rest queue rather than fail.
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>Hashes a password with the currently configured parameters.</summary>
    /// <param name="password">The plaintext, which is never stored or logged.</param>
    /// <param name="cancellationToken">Stops waiting for a turn.</param>
    Task<string> HashAsync(string password, CancellationToken cancellationToken);

    /// <summary>Checks a password against a stored hash, in constant time.</summary>
    /// <param name="password">The plaintext supplied by the caller.</param>
    /// <param name="encodedHash">The stored hash, including its parameters.</param>
    /// <param name="cancellationToken">Stops waiting for a turn.</param>
    Task<PasswordVerification> VerifyAsync(
        string password,
        string encodedHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// A hash of a value nobody knows, for verifying against when the account
    /// does not exist.
    /// </summary>
    /// <remarks>
    /// Sign-in must cost the same whether or not the address is registered.
    /// Skipping the hash for an unknown user makes the response measurably
    /// faster, which turns the login endpoint into an account-enumeration
    /// oracle no matter how careful the error message is.
    /// </remarks>
    string DecoyHash { get; }
}

/// <summary>What verifying a password concluded.</summary>
public enum PasswordVerification
{
    /// <summary>The password does not match.</summary>
    Failed = 0,

    /// <summary>The password matches and the stored hash is current.</summary>
    Valid = 1,

    /// <summary>
    /// The password matches, but the stored hash used weaker parameters than
    /// the configuration now asks for, so it should be replaced.
    /// </summary>
    ValidButNeedsRehash = 2
}
