using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;

namespace Application.Recipes.GetImage;

/// <summary>Writes a recipe's image to a destination.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="Width">Which rendition.</param>
/// <param name="Destination">Where to write it.</param>
public sealed record GetRecipeImageQuery(
    Guid RecipeId,
    Guid UserId,
    int Width,
    Stream Destination);

internal sealed class GetRecipeImageQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IImageStore images)
    : IQueryHandler<GetRecipeImageQuery, ImageDelivery>
{
    public async Task<Result<ImageDelivery>> Handle(
        GetRecipeImageQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetImage");

        // Membership is checked on every image read: an image is exactly as
        // private as the recipe it belongs to, and serving it from a path that
        // skipped the check would make the recipe public by accident.
        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, query.RecipeId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var hash = await visible.Match(
            recipe => recipes.ImageHashAsync(recipe.Id, cancellationToken),
            error => Task.FromResult(Result<string>.Failure(error))).ConfigureAwait(false);

        var result = await hash.Match(
            async contentHash =>
            {
                var written = await images
                    .CopyToAsync(contentHash, query.Width, query.Destination, cancellationToken)
                    .ConfigureAwait(false);

                return written.Map(() => new ImageDelivery(contentHash));
            },
            error => Task.FromResult(Result<ImageDelivery>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

/// <summary>What was served, so the response can carry a cache validator.</summary>
/// <param name="ContentHash">
/// The image's address, which is also its ETag: content-addressed storage means
/// the bytes can never change under the same hash.
/// </param>
public sealed record ImageDelivery(string ContentHash);
