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
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
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

        var result = await ShoppingListWrites
            .ApplyAsync(
                lists,
                unitOfWork,
                command.HouseholdId,
                // Clearing what is bought is the one bulk action worth having:
                // after a shop, removing a dozen ticked lines one at a time is
                // the tedium the list exists to avoid.
                (list, _) => Task.FromResult(
                    command.ItemId is { } id ? list.Remove(id) : Ok(list.ClearChecked())),
                cancellationToken)
            .ConfigureAwait(false);

        return tracked.Record(result);
    }

    private static Result Ok(int removed)
    {
        _ = removed;

        return Result.Success();
    }
}
