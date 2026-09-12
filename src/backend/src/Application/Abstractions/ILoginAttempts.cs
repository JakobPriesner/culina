namespace Application.Abstractions;

/// <summary>
/// Counts failed sign-in attempts against one account.
/// </summary>
/// <remarks>
/// <para>
/// The rate limiter in the pipeline limits per client address, which it must do
/// before the body is read and so cannot see which account is being attacked.
/// This is the other half: a botnet spreading one account's password list
/// across a thousand addresses passes every per-IP limit.
/// </para>
/// <para>
/// The key is a digest of the address, never the address: this lives in memory
/// and appears in diagnostics, and neither should hold a user's email.
/// </para>
/// </remarks>
public interface ILoginAttempts
{
    /// <summary>Whether this account has failed too often to try again yet.</summary>
    /// <param name="accountKey">A digest of the normalised address.</param>
    /// <param name="now">The injected current time.</param>
    bool IsLockedOut(string accountKey, DateTimeOffset now);

    /// <summary>Records a failure.</summary>
    /// <param name="accountKey">A digest of the normalised address.</param>
    /// <param name="now">The injected current time.</param>
    void RecordFailure(string accountKey, DateTimeOffset now);

    /// <summary>Forgets an account's failures after a successful sign-in.</summary>
    /// <param name="accountKey">A digest of the normalised address.</param>
    void Clear(string accountKey);
}
