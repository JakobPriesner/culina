using Application.Abstractions;
using Domain.Shared;

namespace Application.Households;

/// <summary>Runs a membership change, starting over when somebody else changed the household first.</summary>
/// <remarks>
/// Joining, leaving and changing a role all save with the household version that was loaded, which
/// every membership change bumps, so two at once make the second lose the race. The caller sent no
/// <c>If-Match</c>, so that is not their stale view to report: each attempt is one transaction that
/// reads the household, applies the rule and saves, and a lost race simply runs again against the
/// new state. The rules (the last owner may not leave) are re-checked on every attempt, so the
/// version check still guards them.
/// </remarks>
internal static class HouseholdWrites
{
    /// <summary>How many times a change will start over; small, because contention is a few members, not a herd.</summary>
    private const int Attempts = 4;

    internal static async Task<Result> ApplyAsync(
        IUnitOfWork unitOfWork,
        Func<CancellationToken, Task<Result>> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);

        for (var attempt = 1; ; attempt++)
        {
            var outcome = await unitOfWork.InTransactionAsync(change, cancellationToken).ConfigureAwait(false);

            var lost = outcome.Match(() => false, error => error == ConcurrencyErrors.VersionMismatch);

            if (!lost || attempt == Attempts)
            {
                return outcome;
            }
        }
    }

    internal static async Task<Result<TValue>> ApplyAsync<TValue>(
        IUnitOfWork unitOfWork,
        Func<CancellationToken, Task<Result<TValue>>> change,
        CancellationToken cancellationToken)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);

        for (var attempt = 1; ; attempt++)
        {
            var outcome = await unitOfWork.InTransactionAsync(change, cancellationToken).ConfigureAwait(false);

            var lost = outcome.Match(_ => false, error => error == ConcurrencyErrors.VersionMismatch);

            if (!lost || attempt == Attempts)
            {
                return outcome;
            }
        }
    }
}
