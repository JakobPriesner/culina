using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;
using Contracts.Recipes.Intake;

namespace Api.Endpoints.RecipeDrafts.GetIntake.V1;

/// <summary>Reads one import.</summary>
internal sealed class GetRecipeIntakeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipe-intakes/{{id:guid}}", async (Guid id, HttpContext context, IRecipeIntakeJobs jobs, CancellationToken cancellationToken) =>
            {
                var job = await jobs.GetAsync(id, context.CurrentUser().UserId, cancellationToken).ConfigureAwait(false);

                return job is null ? Results.NotFound() : Results.Ok(job);
            })
            .WithName("getRecipeIntakeV1")
            .WithTags(Tags.Recipes)
            .Produces<IntakeJob>()
            .RequireAuthorization();
    }
}
