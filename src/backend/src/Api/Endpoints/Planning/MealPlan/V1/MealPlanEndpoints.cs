using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Planning;
using Contracts.Planning;
using Domain.Planning;
using Domain.Shared;

namespace Api.Endpoints.Planning.MealPlan.V1;

/// <summary>Reads a week of what a household means to cook.</summary>
internal sealed class GetMealPlanEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/meal-plan", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetMealPlanQuery, MealPlanResponse> handler,
                CancellationToken cancellationToken) =>
            {
                // Snapped to the Monday of its week so no caller has to work that out.
                var request = context.Request.Query.ReadDate("from").Map(from =>
                    new GetMealPlanQuery(
                        householdId,
                        context.CurrentUser().UserId,
                        PlanningWeek.StartOfWeekContaining(from.Value ?? DateOnly.FromDateTime(DateTime.UtcNow))));

                return await request.Match(
                    async asked =>
                    {
                        var result = await handler.Handle(asked, cancellationToken).ConfigureAwait(false);

                        return result.Match(Results.Ok, CustomResults.Problem);
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
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

/// <summary>Moves a planned meal to another day.</summary>
internal sealed class MoveMealEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPatch($"{ApiPaths.V1}/households/{{householdId:guid}}/meal-plan/{{entryId:guid}}", async (
                Guid householdId,
                Guid entryId,
                MoveMealRequest request,
                HttpContext context,
                ICommandHandler<MoveMealCommand, MealPlanResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new MoveMealCommand(householdId, context.CurrentUser().UserId, entryId, request),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("moveMealV1")
            .WithTags(Tags.Planning)
            .WithSummary("Move a planned meal")
            .WithDescription(
                "Which day a meal is on is one of its own fields, so moving it is a change to the "
                + "entry rather than an action on it. Omit `slot` to keep the slot it had, and "
                + "`position` to put it at the end of its new day. The whole week comes back, "
                + "because the week is the screen.")
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
