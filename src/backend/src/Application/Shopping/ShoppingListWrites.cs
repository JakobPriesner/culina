using Application.Abstractions;
using Contracts.Shopping;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Shopping;

/// <summary>
/// Changes a household's list, re-reading it when somebody else wrote first.
/// </summary>
/// <remarks>
/// <para>
/// The list is one row with one version, so every write to it is a
/// read-modify-write guarded by that version. Without a retry, two people in
/// the same kitchen adding to the list at the same moment means one of them
/// gets an error and their item is simply not there — and two people shopping
/// together is not an edge case, it is the reason the list is shared.
/// </para>
/// <para>
/// Each attempt runs in one transaction, which is what actually removes the
/// race rather than merely surviving it: reading the list upserts its row, and
/// <c>insert … on conflict do update</c> holds that row until the transaction
/// ends, so a second writer waits for the first instead of reading a version
/// that is about to be stale. The transaction is needed regardless — saving
/// replaces every item row, and a delete that commits without its inserts is an
/// empty shopping list.
/// </para>
/// <para>
/// Retrying is safe here, and is not the "retrying blindly" that
/// <see cref="ConcurrencyErrors"/> warns against: the client never states a
/// version it expects, so the guard is protecting this request's own read, not
/// a precondition the caller asked for. Each attempt re-reads and re-applies,
/// so the merge policy and every item lookup are decided against what is
/// actually stored. A change whose target has genuinely gone — an item somebody
/// else removed — fails with its own error and is not retried.
/// </para>
/// </remarks>
internal static class ShoppingListWrites
{
    /// <summary>
    /// How many times a write will stand aside and try again.
    /// </summary>
    /// <remarks>
    /// Small on purpose. Contention on one household's list is two or three
    /// people, not a thundering herd, and a request that cannot get in after
    /// four reads is reporting something other than a race.
    /// </remarks>
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
