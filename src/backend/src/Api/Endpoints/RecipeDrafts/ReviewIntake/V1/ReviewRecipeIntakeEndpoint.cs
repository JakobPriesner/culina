using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;

namespace Api.Endpoints.RecipeDrafts.ReviewIntake.V1;

/// <summary>Marks an import reviewed, which dismisses it.</summary>
internal sealed class ReviewRecipeIntakeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipe-intakes/{{id:guid}}/reviewed", async (Guid id, HttpContext context, IRecipeIntakeJobs jobs, CancellationToken cancellationToken) =>
            {
                await jobs.ReviewAsync(id, context.CurrentUser().UserId, cancellationToken).ConfigureAwait(false);

                return Results.NoContent();
            })
            .WithName("reviewRecipeIntakeV1")
            .WithTags(Tags.Recipes)
            .RequireAuthorization();
    }
}
