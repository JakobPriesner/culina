using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;

namespace Api.Endpoints.RecipeDrafts.GetIntakePhoto.V1;

/// <summary>Reads one original photograph of an import.</summary>
internal sealed class GetRecipeIntakePhotoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipe-intakes/{{id:guid}}/photos/{{index:int}}", async (
                Guid id,
                int index,
                HttpContext context,
                IRecipeIntakeJobs jobs,
                CancellationToken cancellationToken) =>
            {
                var photo = await jobs.PhotoAsync(id, context.CurrentUser().UserId, index, cancellationToken).ConfigureAwait(false);

                if (photo is null)
                {
                    return Results.NotFound();
                }

                context.Response.Headers.CacheControl = "no-store";

                return Results.Bytes(photo.Bytes, photo.MediaType);
            })
            .WithName("getRecipeIntakePhotoV1")
            .WithTags(Tags.Recipes)
            .RequireAuthorization();
    }
}
