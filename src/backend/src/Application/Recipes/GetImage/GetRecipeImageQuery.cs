using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;

namespace Application.Recipes.GetImage;

/// <summary>Finds a recipe's image, for a caller who may see it.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="Width">Which rendition.</param>
public sealed record GetRecipeImageQuery(Guid RecipeId, Guid UserId, int Width);

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

        // Checked on every read: an image is exactly as private as its recipe.
        var visible = await RecipeAccess
            .VisibleHouseholdAsync(recipes, households, query.RecipeId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var hash = await visible.Match(
            _ => recipes.ImageHashAsync(query.RecipeId, cancellationToken),
            error => Task.FromResult(Result<string>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(hash.Map(contentHash => ImageDelivery.Of(images, contentHash, query.Width)));
    }
}

/// <summary>An image the caller may see, not yet read.</summary>
/// <param name="ContentHash">The image's address, also its ETag: the bytes never change under one hash.</param>
/// <param name="WriteToAsync">Writes the rendition that was asked for.</param>
/// <remarks>The file is read lazily, so a 304 revalidation needs only the hash.</remarks>
public sealed record ImageDelivery(
    string ContentHash,
    Func<Stream, CancellationToken, Task<Result>> WriteToAsync)
{
    internal static ImageDelivery Of(IImageStore images, string contentHash, int width) =>
        new(contentHash, (destination, token) => images.CopyToAsync(contentHash, width, destination, token));
}
