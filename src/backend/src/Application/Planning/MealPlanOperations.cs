using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Recipes;
using Application.Shopping;
using Application.Telemetry;
using Contracts.Planning;
using Domain.Planning;
using Domain.Shared;

namespace Application.Planning;

/// <summary>Reads a week of a household's plan.</summary>
/// <param name="HouseholdId">Whose plan.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="From">Any day of the week to read; this week when null.</param>
public sealed record GetMealPlanQuery(Guid HouseholdId, Guid UserId, DateOnly? From);

/// <summary>Puts a recipe on a day.</summary>
/// <param name="HouseholdId">Whose plan.</param>
/// <param name="UserId">Who is planning.</param>
/// <param name="Draft">What to plan.</param>
public sealed record PlanMealCommand(Guid HouseholdId, Guid UserId, PlanMealRequest Draft);

/// <summary>Takes a planned meal off the week.</summary>
/// <param name="HouseholdId">Whose plan.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="EntryId">Which entry.</param>
public sealed record UnplanMealCommand(Guid HouseholdId, Guid UserId, Guid EntryId);

/// <summary>Moves a planned meal to another day.</summary>
/// <param name="HouseholdId">Whose plan.</param>
/// <param name="UserId">Who is moving it.</param>
/// <param name="EntryId">Which entry.</param>
/// <param name="Draft">Where it goes.</param>
public sealed record MoveMealCommand(
    Guid HouseholdId,
    Guid UserId,
    Guid EntryId,
    MoveMealRequest Draft);

/// <summary>The days a week is read as.</summary>
internal static class PlanWeek
{
    internal const int Days = 7;
}

internal sealed class GetMealPlanQueryHandler(
    IMealPlanRepository plans,
    IHouseholdRepository households,
    TimeProvider time)
    : IQueryHandler<GetMealPlanQuery, MealPlanResponse>
{
    public async Task<Result<MealPlanResponse>> Handle(
        GetMealPlanQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Planning.GetMealPlan");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Snapped to the Monday of its week so no caller has to work that out.
        var asked = query.From ?? DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

        var result = await allowed.Bind(() => PlanningWords.Plannable(asked)).Match(
            async () =>
            {
                var monday = PlanningWeek.StartOfWeekContaining(asked);

                var planned = await plans
                    .ForWeekAsync(query.HouseholdId, monday, PlanWeek.Days, cancellationToken)
                    .ConfigureAwait(false);

                return Result<MealPlanResponse>.Success(planned.ToResponse(monday));
            },
            error => Task.FromResult(Result<MealPlanResponse>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class PlanMealCommandHandler(
    IMealPlanRepository plans,
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IShoppingListRepository lists,
    IIngredientNameRepository names,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PlanMealCommand, MealPlanResponse>
{
    public async Task<Result<MealPlanResponse>> Handle(
        PlanMealCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Planning.PlanMeal");

        // Via the recipe: proves membership and that the recipe is this household's or inherited.
        var recipe = await RecipeAccess
            .VisibleInAsync(
                recipes,
                households,
                command.Draft.RecipeId,
                command.HouseholdId,
                command.UserId,
                cancellationToken)
            .ConfigureAwait(false);

        var planned = await recipe.Match(
            _ => unitOfWork.InTransactionAsync(token => AddAsync(command, token), cancellationToken),
            error => Task.FromResult(Result<MealPlanEntry>.Failure(error))).ConfigureAwait(false);

        var result = await planned.Match(
            async entry =>
            {
                // Leftovers are bought with their original, so nothing on the list is theirs.
                if (entry.LeftoverOf is null)
                {
                    await LinkListedAsync(entry, cancellationToken).ConfigureAwait(false);
                }

                return await ReadWeekAsync(plans, command.HouseholdId, entry.Date, cancellationToken)
                    .ConfigureAwait(false);
            },
            error => Task.FromResult(Result<MealPlanResponse>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<MealPlanEntry>> AddAsync(
        PlanMealCommand command,
        CancellationToken cancellationToken)
    {
        var slot = PlanningWords.Plannable(command.Draft.Date)
            .Bind(() => PlanningWords.ToSlot(command.Draft.Slot));

        return await slot.Match(
            async which =>
            {
                // Leftovers are of a meal of the same recipe on this household's plan, or of nothing.
                if (command.Draft.LeftoverOf is { } original)
                {
                    var of = await plans.FindAsync(original, command.HouseholdId, cancellationToken).ConfigureAwait(false);

                    if (!of.Match(found => found.RecipeId == command.Draft.RecipeId, _ => false))
                    {
                        return PlanningErrors.EntryNotFound;
                    }
                }

                var sortOrder = await plans
                    .NextSortOrderAsync(command.HouseholdId, command.Draft.Date, cancellationToken)
                    .ConfigureAwait(false);

                var entry = MealPlanEntry.Plan(
                    command.HouseholdId,
                    command.Draft.Date,
                    command.Draft.RecipeId,
                    command.Draft.Servings,
                    which,
                    sortOrder,
                    command.Draft.LeftoverOf);

                return await entry.Match(
                    async made =>
                    {
                        await plans.AddAsync(made, cancellationToken).ConfigureAwait(false);

                        return Result<MealPlanEntry>.Success(made);
                    },
                    error => Task.FromResult(Result<MealPlanEntry>.Failure(error)))
                    .ConfigureAwait(false);
            },
            error => Task.FromResult(Result<MealPlanEntry>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>
    /// A recipe already put on the list by itself becomes the shopping for the meal just planned, so the
    /// week does not offer to buy it again (it never doubled, but it looked unshopped) and the list says
    /// which meal it is for. Best effort, after the plan is saved: a lost race leaves it as it was.
    /// </summary>
    private async Task LinkListedAsync(MealPlanEntry entry, CancellationToken cancellationToken)
    {
        var linked = await ShoppingListWrites.ApplyAsync(
            lists,
            names,
            unitOfWork,
            entry.HouseholdId,
            (list, _) =>
            {
                list.CountFor(entry.RecipeId, entry.Id, entry.Date, entry.Slot);

                return Task.FromResult(Result.Success());
            },
            cancellationToken).ConfigureAwait(false);

        linked.Match(_ => { }, _ => { });
    }

    /// <summary>The week containing the changed day, so the client needs no refetch.</summary>
    internal static async Task<Result<MealPlanResponse>> ReadWeekAsync(
        IMealPlanRepository plans,
        Guid householdId,
        DateOnly anyDayOfIt,
        CancellationToken cancellationToken)
    {
        var monday = PlanningWeek.StartOfWeekContaining(anyDayOfIt);

        var planned = await plans
            .ForWeekAsync(householdId, monday, PlanWeek.Days, cancellationToken)
            .ConfigureAwait(false);

        return planned.ToResponse(monday);
    }
}

/// <summary>Moves a planned meal to another day and place, and changes for how many it is when asked; its slot is kept unless one is named.</summary>
internal sealed class MoveMealCommandHandler(
    IMealPlanRepository plans,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<MoveMealCommand, MealPlanResponse>
{
    public async Task<Result<MealPlanResponse>> Handle(
        MoveMealCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Planning.MoveMeal");

        // Membership suffices: the entry is already this household's and its recipe was checked when planned.
        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            () => unitOfWork.InTransactionAsync(
                token => MoveAsync(command, token),
                cancellationToken),
            error => Task.FromResult(Result<MealPlanResponse>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<MealPlanResponse>> MoveAsync(
        MoveMealCommand command,
        CancellationToken cancellationToken)
    {
        var found = await plans
            .FindAsync(command.EntryId, command.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        return await found.Match(
            entry => PlaceAsync(command, entry, cancellationToken),
            error => Task.FromResult(Result<MealPlanResponse>.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Result<MealPlanResponse>> PlaceAsync(
        MoveMealCommand command,
        MealPlanEntry entry,
        CancellationToken cancellationToken)
    {
        if (command.Draft.Position is < 0)
        {
            return PlanningErrors.InvalidPosition;
        }

        // An omitted slot keeps its current one (what dragging sends).
        var slot = PlanningWords.Plannable(command.Draft.Date).Bind(() => command.Draft.Slot is null
            ? Result<MealSlot>.Success(entry.Slot)
            : PlanningWords.ToSlot(command.Draft.Slot));

        return await slot.Match(
            which => ServeAsync(command, entry, which, cancellationToken),
            error => Task.FromResult(Result<MealPlanResponse>.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Result<MealPlanResponse>> ServeAsync(
        MoveMealCommand command,
        MealPlanEntry entry,
        MealSlot slot,
        CancellationToken cancellationToken)
    {
        // An omitted amount keeps the meal's own, as a drag does.
        var served = command.Draft.Servings is { } servings ? entry.WithServings(servings) : entry;

        return await served.Match(
            changed => SaveAsync(command, changed, slot, cancellationToken),
            error => Task.FromResult(Result<MealPlanResponse>.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Result<MealPlanResponse>> SaveAsync(
        MoveMealCommand command,
        MealPlanEntry entry,
        MealSlot slot,
        CancellationToken cancellationToken)
    {
        // Last when no place was asked for: the move sheet only picks a day.
        var sortOrder = command.Draft.Position ?? await plans
            .NextSortOrderAsync(command.HouseholdId, command.Draft.Date, cancellationToken)
            .ConfigureAwait(false);

        var moved = entry.MoveTo(command.Draft.Date, slot, sortOrder);

        await plans.MoveAsync(moved, cancellationToken).ConfigureAwait(false);

        return await PlanMealCommandHandler
            .ReadWeekAsync(plans, command.HouseholdId, moved.Date, cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class UnplanMealCommandHandler(
    IMealPlanRepository plans,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UnplanMealCommand, MealPlanResponse>
{
    public async Task<Result<MealPlanResponse>> Handle(
        UnplanMealCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Planning.UnplanMeal");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            () => unitOfWork.InTransactionAsync(
                async token =>
                {
                    var removed = await plans
                        .RemoveAsync(command.EntryId, command.HouseholdId, token)
                        .ConfigureAwait(false);

                    return await removed.Match(
                        day => PlanMealCommandHandler.ReadWeekAsync(
                            plans, command.HouseholdId, day, token),
                        error => Task.FromResult(Result<MealPlanResponse>.Failure(error)))
                        .ConfigureAwait(false);
                },
                cancellationToken),
            error => Task.FromResult(Result<MealPlanResponse>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
