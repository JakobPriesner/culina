using Application.Abstractions;
using Application.Abstractions.Settings;

namespace Infrastructure.Identity;

/// <summary>
/// A fixed one-minute window of attempts per budget, held in memory.
/// </summary>
/// <remarks>
/// <para>
/// In memory rather than in the database because Culina is a single
/// self-hosted process: a shared store would add a round trip to every sign-in
/// to solve a problem this deployment does not have. A restart forgets the
/// counters, which is an acceptable trade — an attacker cannot cause restarts,
/// and the per-IP limiter still applies. It also forgets which addresses an
/// account signs in from, so until the owner's next success an attack refuses
/// them as it refuses everyone.
/// </para>
/// <para>
/// One lock around every read-modify-write: reserving has to be atomic, or
/// simultaneous guesses each see the count before the others added to it. The
/// work inside is a dictionary lookup, so the lock is never held for long.
/// </para>
/// <para>
/// Entries are pruned opportunistically on write. A dedicated timer would be
/// more code for a map that only grows while an attack is in progress.
/// </para>
/// </remarks>
/// <param name="limits">How many attempts a minute a budget allows.</param>
internal sealed class InMemoryLoginAttempts(RateLimitSettings limits) : ILoginAttempts
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long a successful sign-in vouches for its address: as long as a
    /// session lasts unused by default.
    /// </summary>
    private static readonly TimeSpan KnownFor = TimeSpan.FromDays(30);

    private const int PruneAbove = 1000;

    private readonly Lock gate = new();
    private readonly Dictionary<string, Attempts> attempts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> knownAddresses = new(StringComparer.Ordinal);

    public bool TryReserve(string accountKey, string? clientAddress, DateTimeOffset now)
    {
        lock (gate)
        {
            var budget = BudgetFor(accountKey, clientAddress, now);

            var counted = attempts.TryGetValue(budget, out var existing) && existing.StartedAt.Add(Window) > now
                ? existing with { Count = existing.Count + 1 }
                : new Attempts(now, 1);

            attempts[budget] = counted;
            Prune(now);

            return counted.Count <= limits.LoginPerAccountPerMinute;
        }
    }

    public void Succeeded(string accountKey, string? clientAddress, DateTimeOffset now)
    {
        lock (gate)
        {
            attempts.Remove(BudgetFor(accountKey, clientAddress, now));

            if (clientAddress is not null)
            {
                knownAddresses[Pair(accountKey, clientAddress)] = now;
            }
        }
    }

    /// <summary>
    /// The account's own key, or the account and address together when the
    /// account has recently signed in from there.
    /// </summary>
    private string BudgetFor(string accountKey, string? clientAddress, DateTimeOffset now) =>
        clientAddress is not null
        && knownAddresses.TryGetValue(Pair(accountKey, clientAddress), out var lastSuccess)
        && lastSuccess.Add(KnownFor) > now
            ? Pair(accountKey, clientAddress)
            : accountKey;

    private static string Pair(string accountKey, string clientAddress) => $"{accountKey}@{clientAddress}";

    private void Prune(DateTimeOffset now)
    {
        if (attempts.Count >= PruneAbove)
        {
            foreach (var (key, counted) in attempts)
            {
                if (counted.StartedAt.Add(Window) <= now)
                {
                    attempts.Remove(key);
                }
            }
        }

        if (knownAddresses.Count >= PruneAbove)
        {
            foreach (var (key, lastSuccess) in knownAddresses)
            {
                if (lastSuccess.Add(KnownFor) <= now)
                {
                    knownAddresses.Remove(key);
                }
            }
        }
    }

    private sealed record Attempts(DateTimeOffset StartedAt, int Count);
}
