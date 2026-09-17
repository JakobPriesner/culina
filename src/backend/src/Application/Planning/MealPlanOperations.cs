using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Telemetry;
using Contracts.Planning;
using Domain.Planning;
using Domain.Shared;

namespace Application.Planning;

/// <summary>Reads a week of a household's plan.</summary>
/// <param name="HouseholdId">Whose plan.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="From">The day the week starts on.</param>
public sealed record GetMealPlanQuery(Guid HouseholdId, Guid UserId, DateOnly From);

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

internal sealed class GetMealPlanQueryHandler(IMealPlanRepository plans, IHouseholdRepository households)
    : IQueryHandler<GetMealPlanQuery, MealPlanResponse>
{
    public async Task<Result<MealPlanResponse>> Handle(
        GetMealPlanQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Planning.GetMealPlan");

        var allowed = await RecipeAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () =>
            {
                var planned = await plans
                    .ForWeekAsync(query.HouseholdId, query.From, PlanWeek.Days, cancellationToken)
                    .ConfigureAwait(false);

                return Result<MealPlanResponse>.Success(planned.ToResponse(query.From));
            },
            error => Task.FromResult(Result<MealPlanResponse>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class PlanMealCommandHandler(
    IMealPlanRepository plans,
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PlanMealCommand, MealPlanResponse>
{
    public async Task<Result<MealPlanResponse>> Handle(
        PlanMealCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Planning.PlanMeal");

        // Through the recipe, not the household: it proves in one step both
        // that the caller is a member and that the recipe is one they can see.
        var recipe = await RecipeAccess
            .VisibleAsync(recipes, households, command.Draft.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await recipe.Match(
            found => found.HouseholdId == command.HouseholdId
                ? AddAsync(command, cancellationToken)
                : Task.FromResult(Result<MealPlanResponse>.Failure(
                    Domain.Recipes.RecipeErrors.NotFound(command.Draft.RecipeId))),
            error => Task.FromResult(Result<MealPlanResponse>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private Task<Result<MealPlanResponse>> AddAsync(
        PlanMealCommand command,
        CancellationToken cancellationToken) =>
        unitOfWork.InTransactionAsync(
            async token =>
            {
                var slot = PlanningWords.ToSlot(command.Draft.Slot);

                return await slot.Match(
                    async which =>
                    {
                        var sortOrder = await plans
                            .NextSortOrderAsync(command.HouseholdId, command.Draft.Date, token)
                            .ConfigureAwait(false);

                        var entry = MealPlanEntry.Plan(
                            command.HouseholdId,
                            command.Draft.Date,
                            command.Draft.RecipeId,
                            command.Draft.Servings,
                            which,
                            sortOrder);

                        return await entry.Match(
                            async planned =>
                            {
                                await plans.AddAsync(planned, token).ConfigureAwait(false);

                                return await ReadWeekAsync(
                                    plans, command.HouseholdId, command.Draft.Date, token)
                                    .ConfigureAwait(false);
                            },
                            error => Task.FromResult(Result<MealPlanResponse>.Failure(error)))
                            .ConfigureAwait(false);
                    },
                    error => Task.FromResult(Result<MealPlanResponse>.Failure(error)))
                    .ConfigureAwait(false);
            },
            cancellationToken);

    /// <summary>
    /// The week the changed day belongs to.
    /// </summary>
    /// <remarks>
    /// The week is the screen, so returning it is what saves the client a
    /// refetch to draw anything. It is derived from the day that changed rather
    /// than taken from the request, because the client is looking at the week
    /// that contains it.
    /// </remarks>
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

/// <summary>
/// Moves a planned meal to another day, and to another place in that day.
/// </summary>
/// <remarks>
/// The commonest edit a plan gets: a week is agreed on Sunday and then rearranged
/// all week. Doing it by taking the meal off and putting it back on would lose
/// the servings it was planned for and the slot it was in, which is why this is
/// a move rather than two writes the client stitches together.
/// </remarks>
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

        // Membership is enough: the entry is already this household's, and the
        // recipe on it was checked when it was planned. Nothing here can point
        // the plan at a recipe it could not see before.
        var allowed = await RecipeAccess
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

        // An omitted slot keeps the one it had. That is what dragging sends:
        // dragging a dinner onto Thursday moves a dinner.
        var slot = command.Draft.Slot is null
            ? Result<MealSlot>.Success(entry.Slot)
            : PlanningWords.ToSlot(command.Draft.Slot);

        return await slot.Match(
            which => SaveAsync(command, entry, which, cancellationToken),
            error => Task.FromResult(Result<MealPlanResponse>.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Result<MealPlanResponse>> SaveAsync(
        MoveMealCommand command,
        MealPlanEntry entry,
        MealSlot slot,
        CancellationToken cancellationToken)
    {
        // Last when no place was asked for, which is what the move sheet sends:
        // it answers "which day", and the end of the day is where a meal that
        // was not aimed at a gap belongs.
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

        var allowed = await RecipeAccess
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
