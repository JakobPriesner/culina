namespace Application.Abstractions;

/// <summary>Hashes and verifies passwords.</summary>
/// <remarks>
/// Asynchronous because each hash holds a large block of memory, so only a few run at once and the
/// rest queue.
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>Hashes a password with the currently configured parameters.</summary>
    Task<string> HashAsync(string password, CancellationToken cancellationToken);

    /// <summary>Checks a password against a stored hash, in constant time.</summary>
    Task<PasswordVerification> VerifyAsync(
        string password,
        string encodedHash,
        CancellationToken cancellationToken);

    /// <summary>
    /// A hash of a value nobody knows, to verify against when the account does not exist.
    /// </summary>
    /// <remarks>
    /// Sign-in must cost the same for unknown addresses, or the faster response makes login an
    /// account-enumeration oracle.
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
    /// The password matches, but the stored hash uses weaker parameters than configured and should
    /// be replaced.
    /// </summary>
    ValidButNeedsRehash = 2
}
