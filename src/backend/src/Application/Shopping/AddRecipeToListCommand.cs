using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Telemetry;
using Contracts.Shopping;
using Domain.Shared;

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
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
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

        var overrides = await lists
            .SectionOverridesAsync(command.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        var result = await recipe
            .Match(
                found => ShoppingListWrites.ApplyAsync(
                    lists,
                    unitOfWork,
                    command.HouseholdId,
                    (list, _) => Task.FromResult(
                        RecipeContribution.Add(
                            list,
                            found,
                            command.Servings,
                            planEntryId: null,
                            plannedDate: null,
                            plannedSlot: null,
                            overrides)),
                    cancellationToken),
                error => Task.FromResult(Result<Response>.Failure(error)))
            .ConfigureAwait(false);

        return tracked.Record(result);
    }
}
