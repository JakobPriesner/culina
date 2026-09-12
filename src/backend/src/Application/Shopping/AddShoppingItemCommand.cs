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
    IHouseholdRepository households)
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

        var list = await lists.ForHouseholdAsync(command.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        var overrides = await lists
            .SectionOverridesAsync(command.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        var result = await list.Bind(found => parsed.Map(pair => (List: found, pair.Name, pair.Quantity)))
            .Match(
                bundle => SaveAsync(bundle.List, bundle.Name, bundle.Quantity, overrides, cancellationToken),
                error => Task.FromResult(Result<Response>.Failure(error)))
            .ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> SaveAsync(
        ShoppingList list,
        ItemName name,
        Quantity quantity,
        IReadOnlyDictionary<string, ShoppingSection> overrides,
        CancellationToken cancellationToken)
    {
        var before = list.Version;
        var section = SectionFor(name, overrides);

        // A hand-typed line is not merged: somebody typing "butter" when butter
        // is already on the list usually means they want more of it noted
        // separately, and merging silently would hide that they added anything.
        var added = list.AddManual(name, quantity, section);

        var saved = await added
            .Match(
                _ => lists.SaveAsync(list, before, cancellationToken),
                error => Task.FromResult(Result.Failure(error)))
            .ConfigureAwait(false);

        return saved.Map(() => list.Describe());
    }

    /// <summary>
    /// Where this household keeps a thing, or where a shop usually does.
    /// </summary>
    /// <remarks>
    /// A correction always wins: the seeded guess is a default, and a person who
    /// has moved an item once should not have to move it again.
    /// </remarks>
    internal static ShoppingSection SectionFor(
        ItemName name,
        IReadOnlyDictionary<string, ShoppingSection> overrides) =>
        overrides.TryGetValue(name.ComparisonKey, out var corrected)
            ? corrected
            : SectionKeywords.SectionFor(name);
}
