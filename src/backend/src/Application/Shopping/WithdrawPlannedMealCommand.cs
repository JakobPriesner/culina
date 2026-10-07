using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Shopping;
using Domain.Households;
using Domain.Shared;

namespace Application.Shopping;

/// <summary>Takes a planned meal's shopping off the list: only what that meal added; typed lines and bought items stay. Idempotent.</summary>
/// <param name="HouseholdId">Whose list.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="PlanEntryId">The planned meal, which may already be off the plan.</param>
public sealed record WithdrawPlannedMealCommand(Guid HouseholdId, Guid UserId, Guid PlanEntryId);

internal sealed class WithdrawPlannedMealCommandHandler(
    IShoppingListRepository lists,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<WithdrawPlannedMealCommand, Response>
{
    public async Task<Result<Response>> Handle(
        WithdrawPlannedMealCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Shopping.WithdrawPlannedMeal");

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
                (list, _) =>
                {
                    list.Withdraw(command.PlanEntryId);

                    return Task.FromResult(Result.Success());
                },
                cancellationToken)
            .ConfigureAwait(false);

        return tracked.Record(result);
    }
}
