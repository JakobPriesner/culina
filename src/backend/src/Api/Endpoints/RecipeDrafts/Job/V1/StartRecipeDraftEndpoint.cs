using Api.Extensions;
using Api.Infrastructure;
using Application.Recipes.Intake;
using Contracts.Recipes.Intake;
using Request = Contracts.Recipes.Drafts.Request;

namespace Api.Endpoints.RecipeDrafts.Job.V1;

/// <summary>Asks the assistant for a recipe as a server job, which leaving the app does not stop.</summary>
internal sealed class StartRecipeDraftEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/recipe-drafts/{{id:guid}}", async (
                Guid id,
                Request request,
                HttpContext context,
                RecipeIntake intake,
                CancellationToken cancellationToken) =>
            {
                var result = await intake
                    .StartDraftAsync(
                        id,
                        context.CurrentUser().UserId,
                        request.HouseholdId,
                        request.Kind,
                        request.Material,
                        request.RecipeId,
                        request.Language,
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    job => Results.Accepted($"{ApiPaths.V1}/recipe-intakes/{job.Id}", job),
                    CustomResults.Problem);
            })
            .WithName("startRecipeDraftV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Ask the assistant for a recipe, as a job")
            .WithDescription(
                "The same draft `POST /recipe-drafts` streams, written by the server's intake worker instead of the "
                + "request, so a phone putting the app away does not cut it (docs/native-app-design.md, part 8). "
                + "`kind` is `idea` or `revision`; nothing is created, and the draft is shown for correction as before.\n\n"
                + "The caller picks the id, so asking again after a lost answer is the same job, not a second bill. "
                + "202 with the job; its draft grows on `GET /recipe-intakes/events` (`purpose: draft`) and ends at "
                + "`ready`, or `failed` with an error code. `POST /recipe-intakes/{id}/reviewed` dismisses it, and the "
                + "worker stops writing at its next step. Starting a draft dismisses the person's finished earlier ones.\n\n"
                + "Refused before anything is queued: 404 when this instance has no assistant for the job, or the recipe "
                + "to rewrite is not this household's; 400 with nothing to work from or too much. The month's budget is "
                + "decided when the job runs, as the job's error code.")
            .Produces<IntakeJob>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireRateLimiting(RateLimitExtensions.Assistance)
            .RequireAuthorization();
    }
}
