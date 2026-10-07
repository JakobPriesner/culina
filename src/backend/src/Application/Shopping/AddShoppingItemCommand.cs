using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Telemetry;
using Contracts.Shopping;
using Domain.Households;
using Domain.Recipes;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Shopping;

/// <summary>Puts something on the list by hand.</summary>
/// <param name="HouseholdId">Whose list.</param>
/// <param name="UserId">Who is adding it.</param>
/// <param name="Name">What to buy.</param>
/// <param name="Quantity">How much, if they said.</param>
/// <param name="Unit">In what.</param>
public sealed record AddShoppingItemCommand(
    Guid HouseholdId,
    Guid UserId,
    string Name,
    decimal? Quantity,
    string? Unit);

internal sealed class AddShoppingItemCommandHandler(
    IShoppingListRepository lists,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddShoppingItemCommand, Response>
{
    public async Task<Result<Response>> Handle(
        AddShoppingItemCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Shopping.AddItem");

        var member = await households
            .IsMemberAsync(command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!member)
        {
            return tracked.Record(
                Result<Response>.Failure(HouseholdErrors.NotFound(command.HouseholdId)));
        }

        var parsed = ItemName.Create(command.Name)
            .Bind(name => RecipeWords.ToQuantity(command.Quantity, command.Unit)
                .Map(quantity => (Name: name, Quantity: quantity)));

        var overrides = await lists
            .SectionOverridesAsync(command.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        var result = await parsed
            .Match(
                pair => ShoppingListWrites.ApplyAsync(
                    lists,
                    unitOfWork,
                    command.HouseholdId,
                    // A hand-typed line is not merged, or the user would not see that they added anything.
                    (list, _) => Task.FromResult(
                        list.AddManual(pair.Name, pair.Quantity, SectionFor(pair.Name, overrides))
                            .Bind(_ => Result.Success())),
                    cancellationToken),
                error => Task.FromResult(Result<Response>.Failure(error)))
            .ConfigureAwait(false);

        return tracked.Record(result);
    }

    // A household's own correction always wins over the keyword default.
    internal static ShoppingSection SectionFor(
        ItemName name,
        IReadOnlyDictionary<string, ShoppingSection> overrides) =>
        overrides.TryGetValue(name.ComparisonKey, out var corrected)
            ? corrected
            : SectionKeywords.SectionFor(name);
}
