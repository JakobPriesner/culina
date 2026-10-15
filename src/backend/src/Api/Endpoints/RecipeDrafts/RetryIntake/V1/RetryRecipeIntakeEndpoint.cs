using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Intake;
using Contracts.Recipes.Intake;

namespace Api.Endpoints.RecipeDrafts.RetryIntake.V1;

/// <summary>Tries a failed import again as a new job.</summary>
internal sealed class RetryRecipeIntakeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipe-intakes/{{id:guid}}/retry", async (
                Guid id,
                Guid nextId,
                HttpContext context,
                ICommandHandler<RetryRecipeIntakeCommand, IntakeJob> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new RetryRecipeIntakeCommand(id, nextId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(job => Results.Accepted($"{ApiPaths.V1}/recipe-intakes/{job.Id}", job), CustomResults.Problem);
            })
            .WithQueryParameters("nextId")
            .WithName("retryRecipeIntakeV1")
            .WithTags(Tags.Recipes)
            .Produces<IntakeJob>(202)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitExtensions.Assistance);
    }
}
