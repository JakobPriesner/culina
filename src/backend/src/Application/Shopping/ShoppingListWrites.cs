using Application.Abstractions;
using Contracts.Shopping;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Shopping;

/// <summary>Changes a household's list, re-reading it when somebody else wrote first.</summary>
/// <remarks>
/// A guarded read-modify-write on a one-row list; two people shopping together is why it is shared,
/// so it retries instead of erroring. Each attempt is one transaction: reading upserts the row
/// (<c>on conflict do update</c> holds it), so a second writer waits rather than reading a stale
/// version. Retrying is safe, unlike the blind retries <see cref="ConcurrencyErrors"/> warns
/// against: the client states no expected version, and a change whose target is gone fails with its
/// own error, unretried.
/// </remarks>
internal static class ShoppingListWrites
{
    /// <summary>
    /// How many times a write will stand aside and try again; small, because contention is two or
    /// three people, not a herd.
    /// </summary>
    private const int Attempts = 4;

    internal static async Task<Result<Response>> ApplyAsync(
        IShoppingListRepository lists,
        IUnitOfWork unitOfWork,
        Guid householdId,
        Func<ShoppingList, CancellationToken, Task<Result>> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);

        for (var attempt = 1; ; attempt++)
        {
            var outcome = await unitOfWork
                .InTransactionAsync(
                    token => OnceAsync(lists, householdId, change, token),
                    cancellationToken)
                .ConfigureAwait(false);

            var lost = outcome.Match(
                _ => false,
                error => error == ConcurrencyErrors.VersionMismatch);

            if (!lost || attempt == Attempts)
            {
                return outcome;
            }
        }
    }

    private static async Task<Result<Response>> OnceAsync(
        IShoppingListRepository lists,
        Guid householdId,
        Func<ShoppingList, CancellationToken, Task<Result>> change,
        CancellationToken cancellationToken)
    {
        var found = await lists.ForHouseholdAsync(householdId, cancellationToken)
            .ConfigureAwait(false);

        return await found
            .Match(
                list => SaveAsync(lists, list, change, cancellationToken),
                error => Task.FromResult(Result<Response>.Failure(error)))
            .ConfigureAwait(false);
    }

    private static async Task<Result<Response>> SaveAsync(
        IShoppingListRepository lists,
        ShoppingList list,
        Func<ShoppingList, CancellationToken, Task<Result>> change,
        CancellationToken cancellationToken)
    {
        var before = list.Version;

        var changed = await change(list, cancellationToken).ConfigureAwait(false);

        var saved = await changed
            .Match(
                () => lists.SaveAsync(list, before, cancellationToken),
                error => Task.FromResult(Result.Failure(error)))
            .ConfigureAwait(false);

        return saved.Map(() => list.Describe());
    }
}
