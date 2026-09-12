using System.Collections.Concurrent;
using Application.Abstractions;
using Application.Abstractions.Settings;

namespace Infrastructure.Identity;

/// <summary>
/// A fixed one-minute window of failures per account, held in memory.
/// </summary>
/// <remarks>
/// <para>
/// In memory rather than in the database because Culina is a single
/// self-hosted process: a shared store would add a round trip to every sign-in
/// to solve a problem this deployment does not have. A restart forgets the
/// counters, which is an acceptable trade — an attacker cannot cause restarts,
/// and the per-IP limiter still applies.
/// </para>
/// <para>
/// Entries are pruned opportunistically on write. A dedicated timer would be
/// more code for a map that only grows while an attack is in progress.
/// </para>
/// </remarks>
/// <param name="limits">How many failures a minute the account may have.</param>
internal sealed class InMemoryLoginAttempts(RateLimitSettings limits) : ILoginAttempts
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Attempts> failures = new(StringComparer.Ordinal);

    public bool IsLockedOut(string accountKey, DateTimeOffset now) =>
        failures.TryGetValue(accountKey, out var attempts)
        && attempts.StartedAt.Add(Window) > now
        && attempts.Count >= limits.LoginPerAccountPerMinute;

    public void RecordFailure(string accountKey, DateTimeOffset now)
    {
        failures.AddOrUpdate(
            accountKey,
            _ => new Attempts(now, 1),
            (_, existing) => existing.StartedAt.Add(Window) <= now
                ? new Attempts(now, 1)
                : existing with { Count = existing.Count + 1 });

        Prune(now);
    }

    public void Clear(string accountKey) => failures.TryRemove(accountKey, out _);

    private void Prune(DateTimeOffset now)
    {
        if (failures.Count < 1000)
        {
            return;
        }

        foreach (var (key, attempts) in failures)
        {
            if (attempts.StartedAt.Add(Window) <= now)
            {
                failures.TryRemove(key, out _);
            }
        }
    }

    private sealed record Attempts(DateTimeOffset StartedAt, int Count);
}
