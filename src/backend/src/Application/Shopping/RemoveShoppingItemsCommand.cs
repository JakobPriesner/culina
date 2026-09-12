using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Shopping;
using Domain.Households;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Shopping;

/// <summary>Takes a line off the list, or clears what has been bought.</summary>
/// <param name="HouseholdId">Whose list.</param>
/// <param name="UserId">Who is clearing it.</param>
/// <param name="ItemId">Which line, or null to clear everything ticked.</param>
public sealed record RemoveShoppingItemsCommand(Guid HouseholdId, Guid UserId, Guid? ItemId);

internal sealed class RemoveShoppingItemsCommandHandler(
    IShoppingListRepository lists,
    IHouseholdRepository households)
    : ICommandHandler<RemoveShoppingItemsCommand, Response>
{
    public async Task<Result<Response>> Handle(
        RemoveShoppingItemsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Shopping.RemoveItems");

        var member = await households
            .IsMemberAsync(command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!member)
        {
            return tracked.Record(
                Result<Response>.Failure(HouseholdErrors.NotFound(command.HouseholdId)));
        }

        var list = await lists.ForHouseholdAsync(command.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        var result = await list
            .Match(
                found => RemoveAsync(found, command.ItemId, cancellationToken),
                error => Task.FromResult(Result<Response>.Failure(error)))
            .ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> RemoveAsync(
        ShoppingList list,
        Guid? itemId,
        CancellationToken cancellationToken)
    {
        var before = list.Version;

        // Clearing what is bought is the one bulk action worth having: after a
        // shop, removing a dozen ticked lines one at a time is the tedium the
        // list exists to avoid.
        var removed = itemId is { } id ? list.Remove(id) : Ok(list.ClearChecked());

        var saved = await removed
            .Match(
                () => lists.SaveAsync(list, before, cancellationToken),
                error => Task.FromResult(Result.Failure(error)))
            .ConfigureAwait(false);

        return saved.Map(() => list.Describe());
    }

    private static Result Ok(int removed)
    {
        _ = removed;

        return Result.Success();
    }
}
