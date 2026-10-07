using Application.Abstractions;
using Application.Abstractions.Settings;

namespace Infrastructure.Identity;

/// <summary>
/// A fixed one-minute window of attempts per budget. In memory because Culina is a single process; a restart
/// forgets the counters, which an attacker cannot trigger. One lock keeps reserve atomic; pruning happens on write.
/// </summary>
/// <param name="limits">How many attempts a minute a budget allows.</param>
internal sealed class InMemoryLoginAttempts(RateLimitSettings limits) : ILoginAttempts
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    // How long a successful sign-in vouches for its address (the default unused session lifetime).
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

    // The account's key, or account plus address when it recently signed in from there.
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
