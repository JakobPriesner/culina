namespace Application.Abstractions;

/// <summary>
/// Counts attempts at one account's secrets.
/// </summary>
/// <remarks>
/// <para>
/// The rate limiter in the pipeline limits per client address, which it must do
/// before the body is read and so cannot see which account is being attacked.
/// This is the other half: a botnet spreading one account's password list
/// across a thousand addresses passes every per-IP limit.
/// </para>
/// <para>
/// An attempt is counted before the secret is checked, not after it failed.
/// Counting afterwards let a burst of simultaneous guesses all pass the check
/// before the first failure was written down.
/// </para>
/// <para>
/// The budget is the account's, shared by every address — except for an
/// address the account has signed in from before, which gets a budget of its
/// own. A lockout that refused everyone would let anybody keep the owner out
/// for good with five wrong guesses a minute; this way the guesses still count
/// against the account, but the owner at home is not the one who pays for
/// them.
/// </para>
/// <para>
/// The key is a digest of the address, never the address: this lives in memory
/// and appears in diagnostics, and neither should hold a user's email.
/// </para>
/// </remarks>
public interface ILoginAttempts
{
    /// <summary>
    /// Takes one attempt from the budget this caller draws on, before the
    /// secret is checked.
    /// </summary>
    /// <param name="accountKey">A digest of the normalised address.</param>
    /// <param name="clientAddress">Where the attempt comes from, if known.</param>
    /// <param name="now">The injected current time.</param>
    /// <returns>False when that budget is spent for now; the secret must then not be checked.</returns>
    bool TryReserve(string accountKey, string? clientAddress, DateTimeOffset now);

    /// <summary>
    /// Forgets the attempts counted against this caller's budget, and
    /// remembers the client address as one the account signs in from.
    /// </summary>
    /// <param name="accountKey">A digest of the normalised address.</param>
    /// <param name="clientAddress">Where the successful attempt came from, if known.</param>
    /// <param name="now">The injected current time.</param>
    void Succeeded(string accountKey, string? clientAddress, DateTimeOffset now);
}
