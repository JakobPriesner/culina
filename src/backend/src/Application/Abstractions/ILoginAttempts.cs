namespace Application.Abstractions;

/// <summary>Counts attempts at one account's secrets, the per-account half of the per-IP rate limit.</summary>
/// <remarks>
/// An attempt is counted before the secret is checked, so a burst of simultaneous guesses cannot all pass first.
/// The budget is the account's, except an address it has signed in from before gets its own, so the owner at home does not pay for an attacker's guesses.
/// The key is a digest, never the email: this lives in memory and diagnostics.
/// </remarks>
public interface ILoginAttempts
{
    /// <summary>Takes one attempt from the budget this caller draws on, before the secret is checked.</summary>
    /// <param name="accountKey">A digest of the normalised address.</param>
    /// <param name="clientAddress">Where the attempt comes from, if known.</param>
    /// <param name="now">The injected current time.</param>
    /// <returns>False when that budget is spent for now; the secret must then not be checked.</returns>
    bool TryReserve(string accountKey, string? clientAddress, DateTimeOffset now);

    /// <summary>Forgets the attempts counted against this caller's budget, and remembers the address as one the account signs in from.</summary>
    /// <param name="accountKey">A digest of the normalised address.</param>
    /// <param name="clientAddress">Where the successful attempt came from, if known.</param>
    /// <param name="now">The injected current time.</param>
    void Succeeded(string accountKey, string? clientAddress, DateTimeOffset now);
}
