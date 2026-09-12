namespace Application.Abstractions;

/// <summary>
/// Hashes and verifies passwords.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a password with the currently configured parameters.</summary>
    /// <param name="password">The plaintext, which is never stored or logged.</param>
    string Hash(string password);

    /// <summary>Checks a password against a stored hash, in constant time.</summary>
    /// <param name="password">The plaintext supplied by the caller.</param>
    /// <param name="encodedHash">The stored hash, including its parameters.</param>
    PasswordVerification Verify(string password, string encodedHash);

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
