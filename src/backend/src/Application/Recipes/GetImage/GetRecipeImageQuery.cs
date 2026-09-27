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

        // Membership is checked on every image read: an image is exactly as
        // private as the recipe it belongs to, and serving it from a path that
        // skipped the check would make the recipe public by accident.
        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, query.RecipeId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var hash = await visible.Match(
            recipe => recipes.ImageHashAsync(recipe.Id, cancellationToken),
            error => Task.FromResult(Result<string>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(hash.Map(contentHash => ImageDelivery.Of(images, contentHash, query.Width)));
    }
}

/// <summary>An image the caller may see, not yet read.</summary>
/// <param name="ContentHash">
/// The image's address, which is also its ETag: content-addressed storage means
/// the bytes can never change under the same hash.
/// </param>
/// <param name="WriteToAsync">Writes the rendition that was asked for.</param>
/// <remarks>
/// The access check and the hash lookup are done by the time this exists; the
/// file is not. Most image reads are a browser revalidating a picture it
/// already has, and answering those with 304 needs only the hash — reading the
/// file first, as this used to, paid a full disk read to throw it away.
/// </remarks>
public sealed record ImageDelivery(
    string ContentHash,
    Func<Stream, CancellationToken, Task<Result>> WriteToAsync)
{
    internal static ImageDelivery Of(IImageStore images, string contentHash, int width) =>
        new(contentHash, (destination, token) => images.CopyToAsync(contentHash, width, destination, token));
}
