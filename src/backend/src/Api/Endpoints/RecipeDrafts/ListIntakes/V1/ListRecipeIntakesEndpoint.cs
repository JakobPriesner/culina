using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;
using Contracts.Recipes.Intake;

namespace Api.Endpoints.RecipeDrafts.ListIntakes.V1;

/// <summary>Lists the person's unreviewed imports.</summary>
internal sealed class ListRecipeIntakesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipe-intakes", async (HttpContext context, IRecipeIntakeJobs jobs, CancellationToken cancellationToken) =>
                Results.Ok(await jobs.ListAsync(context.CurrentUser().UserId, cancellationToken).ConfigureAwait(false)))
            .WithName("listRecipeIntakesV1")
            .WithTags(Tags.Recipes)
            .Produces<IReadOnlyList<IntakeJob>>()
            .RequireAuthorization();
    }
}
