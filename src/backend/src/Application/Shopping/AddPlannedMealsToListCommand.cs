using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Planning;
using Application.Telemetry;
using Contracts.Shopping;
using Domain.Households;
using Domain.Recipes;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Shopping;

/// <summary>Puts a planned week's shopping on the list — each meal once.</summary>
/// <param name="HouseholdId">Whose plan and list.</param>
/// <param name="UserId">Who is shopping.</param>
/// <param name="From">The day the week starts on.</param>
/// <remarks>
/// Safe to repeat, which is the point. A meal already on the list is skipped,
/// and a recipe that went on the list by itself counts as the shopping for a
/// planned meal of it — so adding the week after adding the waffles from their
/// recipe does not buy the waffles twice. Two planned meals of the same recipe
/// are two meals, and each is shopped for.
/// </remarks>
public sealed record AddPlannedMealsToListCommand(Guid HouseholdId, Guid UserId, DateOnly From);

internal sealed class AddPlannedMealsToListCommandHandler(
    IShoppingListRepository lists,
    IMealPlanRepository plans,
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddPlannedMealsToListCommand, Response>
{
    public async Task<Result<Response>> Handle(
        AddPlannedMealsToListCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Shopping.AddPlannedMeals");

        var member = await households
            .IsMemberAsync(command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!member)
        {
            return tracked.Record(
                Result<Response>.Failure(HouseholdErrors.NotFound(command.HouseholdId)));
        }

        var week = await plans
            .ForWeekAsync(command.HouseholdId, command.From, PlanWeek.Days, cancellationToken)
            .ConfigureAwait(false);

        var cooked = await RecipesOfAsync(week, cancellationToken).ConfigureAwait(false);

        var overrides = await lists
            .SectionOverridesAsync(command.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        var result = await ShoppingListWrites
            .ApplyAsync(
                lists,
                unitOfWork,
                command.HouseholdId,
                (list, _) => Task.FromResult(Shop(list, week, cooked, overrides)),
                cancellationToken)
            .ConfigureAwait(false);

        return tracked.Record(result);
    }

    private static Result Shop(
        ShoppingList list,
        IReadOnlyList<PlannedRecipe> week,
        IReadOnlyDictionary<Guid, Recipe> cooked,
        IReadOnlyDictionary<string, ShoppingSection> overrides)
    {
        var outcome = Result.Success();

        foreach (var planned in week)
        {
            var entry = planned.Entry;

            if (list.IsShoppedFor(entry.Id) || list.CountFor(entry.RecipeId, entry.Id))
            {
                continue;
            }

            // A recipe deleted since the week was read has nothing left to buy.
            if (!cooked.TryGetValue(entry.RecipeId, out var recipe))
            {
                continue;
            }

            outcome = outcome.Bind(() => RecipeContribution.Add(
                list,
                recipe,
                entry.Servings ?? recipe.Yield.Amount,
                entry.Id,
                overrides));
        }

        return outcome;
    }

    /// <summary>Each recipe on the week, read once however often it is planned.</summary>
    private async Task<IReadOnlyDictionary<Guid, Recipe>> RecipesOfAsync(
        IReadOnlyList<PlannedRecipe> week,
        CancellationToken cancellationToken)
    {
        var cooked = new Dictionary<Guid, Recipe>();

        foreach (var recipeId in week.Select(planned => planned.Entry.RecipeId).Distinct())
        {
            var found = await recipes.FindAsync(recipeId, cancellationToken).ConfigureAwait(false);

            found.Match(recipe => cooked[recipeId] = recipe, _ => { });
        }

        return cooked;
    }
}
