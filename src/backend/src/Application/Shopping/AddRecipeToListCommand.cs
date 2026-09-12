using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Telemetry;
using Contracts.Shopping;
using Domain.Recipes;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Shopping;

/// <summary>Puts a recipe's ingredients on the list, at the scaling being cooked.</summary>
/// <param name="HouseholdId">Whose list.</param>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is adding it.</param>
/// <param name="Servings">How many it is being made for.</param>
public sealed record AddRecipeToListCommand(
    Guid HouseholdId,
    Guid RecipeId,
    Guid UserId,
    decimal Servings);

internal sealed class AddRecipeToListCommandHandler(
    IShoppingListRepository lists,
    IRecipeRepository recipes,
    IHouseholdRepository households)
    : ICommandHandler<AddRecipeToListCommand, Response>
{
    public async Task<Result<Response>> Handle(
        AddRecipeToListCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Shopping.AddRecipe");

        var recipe = await RecipeAccess
            .VisibleAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var list = await lists.ForHouseholdAsync(command.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        var overrides = await lists
            .SectionOverridesAsync(command.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        var result = await recipe.Bind(found => list.Map(target => (Recipe: found, List: target)))
            .Match(
                pair => SaveAsync(pair.List, pair.Recipe, command.Servings, overrides, cancellationToken),
                error => Task.FromResult(Result<Response>.Failure(error)))
            .ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> SaveAsync(
        ShoppingList list,
        Recipe recipe,
        decimal servings,
        IReadOnlyDictionary<string, ShoppingSection> overrides,
        CancellationToken cancellationToken)
    {
        var before = list.Version;

        // Exact decimal arithmetic, and the sum is stored unrounded. A recipe
        // for two scaled to five contributes 2.5 × its amounts, and three such
        // recipes must add up to what they actually add up to — rounding each
        // one first would compound the error into a number nobody asked for.
        var factor = recipe.Yield.Amount > 0 ? servings / recipe.Yield.Amount : 1m;

        foreach (var ingredient in recipe.Groups.SelectMany(group => group.Ingredients))
        {
            var scaled = Scale(ingredient.Quantity, factor);
            var name = ItemName.Create(ingredient.Name);

            var added = name.Bind(itemName => list.Add(
                itemName,
                scaled,
                AddShoppingItemCommandHandler.SectionFor(itemName, overrides)));

            if (added.Match(_ => false, _ => true))
            {
                return added.Map(_ => list.Describe());
            }
        }

        var saved = await lists.SaveAsync(list, before, cancellationToken).ConfigureAwait(false);

        return saved.Map(() => list.Describe());
    }

    /// <summary>
    /// Multiplies an amount, leaving alone the ones that do not scale.
    /// </summary>
    /// <remarks>
    /// A pinch is a gesture: doubling a recipe does not double it. An
    /// ingredient with no amount has nothing to multiply.
    /// </remarks>
    private static Quantity Scale(Quantity quantity, decimal factor)
    {
        if (!quantity.Scales || quantity.Amount is not { } amount)
        {
            return quantity;
        }

        return Quantity.Create(amount * factor, quantity.Unit)
            .Match(scaled => scaled, _ => quantity);
    }
}
