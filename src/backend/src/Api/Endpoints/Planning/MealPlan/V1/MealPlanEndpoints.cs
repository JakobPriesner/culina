using System.Globalization;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Planning;
using Contracts.Planning;
using Domain.Planning;

namespace Api.Endpoints.Planning.MealPlan.V1;

/// <summary>Reads a week of what a household means to cook.</summary>
internal sealed class GetMealPlanEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/meal-plan", async (
                Guid householdId,
                string? from,
                HttpContext context,
                IQueryHandler<GetMealPlanQuery, MealPlanResponse> handler,
                CancellationToken cancellationToken) =>
            {
                if (!TryReadFrom(from, out var start))
                {
                    return CustomResults.Problem(PlanningErrors.InvalidRange);
                }

                var result = await handler
                    .Handle(
                        new GetMealPlanQuery(householdId, context.CurrentUser().UserId, start),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getMealPlanV1")
            .WithTags(Tags.Planning)
            .WithSummary("Read a week of the plan")
            .WithDescription(
                "Seven days from the Monday of the week containing `from`, or of this week when it "
                + "is omitted. Always all seven, planned or not: a week with holes in it is a week "
                + "the client has to fill in itself.")
            .WithQueryParameters("from")
            .Produces<MealPlanResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    /// <summary>
    /// Reads the day asked for, snapped to the Monday of its week.
    /// </summary>
    /// <remarks>
    /// Snapped here rather than demanded of the client: asking every caller to
    /// work out which Monday a Thursday belongs to is asking for the one that
    /// gets it wrong.
    /// </remarks>
    private static bool TryReadFrom(string? from, out DateOnly start)
    {
        if (string.IsNullOrEmpty(from))
        {
            start = PlanningWeek.StartOfWeekContaining(DateOnly.FromDateTime(DateTime.UtcNow));

            return true;
        }

        if (!DateOnly.TryParse(from, CultureInfo.InvariantCulture, out var day))
        {
            start = default;

            return false;
        }

        start = PlanningWeek.StartOfWeekContaining(day);

        return true;
    }
}

/// <summary>Puts a recipe on a day.</summary>
internal sealed class PlanMealEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/households/{{householdId:guid}}/meal-plan", async (
                Guid householdId,
                PlanMealRequest request,
                HttpContext context,
                ICommandHandler<PlanMealCommand, MealPlanResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new PlanMealCommand(householdId, context.CurrentUser().UserId, request),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("planMealV1")
            .WithTags(Tags.Planning)
            .WithSummary("Plan a meal")
            .WithDescription(
                "Omit `servings` for however many the recipe was written for, and `slot` for "
                + "dinner. The whole week comes back, because the week is the screen.")
            .Produces<MealPlanResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}

/// <summary>Takes a planned meal off the week.</summary>
internal sealed class UnplanMealEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}/meal-plan/{{entryId:guid}}", async (
                Guid householdId,
                Guid entryId,
                HttpContext context,
                ICommandHandler<UnplanMealCommand, MealPlanResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new UnplanMealCommand(householdId, context.CurrentUser().UserId, entryId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("unplanMealV1")
            .WithTags(Tags.Planning)
            .WithSummary("Take a meal off the plan")
            .WithDescription("The week it was in comes back, because the week is the screen.")
            .Produces<MealPlanResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
