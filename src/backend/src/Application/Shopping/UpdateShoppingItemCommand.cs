using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Shopping;
using Domain.Households;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Shopping;

/// <summary>Ticks a line off, or moves it to where it actually lives.</summary>
/// <param name="HouseholdId">Whose list.</param>
/// <param name="ItemId">Which line.</param>
/// <param name="UserId">Who is changing it.</param>
/// <param name="IsChecked">Whether it is now in the trolley.</param>
/// <param name="Section">Where it actually belongs.</param>
public sealed record UpdateShoppingItemCommand(
    Guid HouseholdId,
    Guid ItemId,
    Guid UserId,
    bool? IsChecked,
    string? Section);

internal sealed class UpdateShoppingItemCommandHandler(
    IShoppingListRepository lists,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<UpdateShoppingItemCommand, Response>
{
    public async Task<Result<Response>> Handle(
        UpdateShoppingItemCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Shopping.UpdateItem");

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
                (list, token) => ChangeAsync(list, command, token),
                cancellationToken)
            .ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result> ChangeAsync(
        ShoppingList list,
        UpdateShoppingItemCommand command,
        CancellationToken cancellationToken)
    {
        var changed = command.IsChecked is { } isChecked
            ? list.Check(command.ItemId, isChecked, time.GetUtcNow())
            : Result.Success();

        if (command.Section is { } section)
        {
            var moved = changed
                .Bind(() => ShoppingWords.ToSection(section))
                .Bind(parsed => list.MoveToSection(command.ItemId, parsed).Map(() => parsed));

            changed = await moved
                .Match(
                    parsed => RememberAsync(list, command, parsed, cancellationToken),
                    error => Task.FromResult(Result.Failure(error)))
                .ConfigureAwait(false);
        }

        return changed;
    }

    /// <summary>
    /// Remembers the correction, so it is never made twice.
    /// </summary>
    /// <remarks>
    /// This is what replaces a configuration screen: the guess is a default,
    /// and moving an item once teaches the household where it lives.
    /// </remarks>
    private async Task<Result> RememberAsync(
        ShoppingList list,
        UpdateShoppingItemCommand command,
        ShoppingSection section,
        CancellationToken cancellationToken)
    {
        var item = list.Items.FirstOrDefault(candidate => candidate.Id == command.ItemId);

        if (item is null)
        {
            return ShoppingErrors.ItemNotFound;
        }

        return await lists
            .RememberSectionAsync(command.HouseholdId, item.Name.ComparisonKey, section, cancellationToken)
            .ConfigureAwait(false);
    }
}
