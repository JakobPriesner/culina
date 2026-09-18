using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes.GetImage;
using Application.Telemetry;
using Domain.Shared;

namespace Application.Recipes.GetSharedImage;

/// <summary>Writes a published recipe's photograph to a destination.</summary>
/// <remarks>
/// Its own query rather than a flag on <see cref="GetRecipeImageQuery"/>,
/// because that one's whole job is to check household membership on every
/// single image read. A parameter that switched the check off is the one thing
/// that comment warns against; resolving the token to a recipe here keeps the
/// two paths separate and each of them unconditional.
/// </remarks>
/// <param name="Token">The secret out of the link.</param>
/// <param name="Width">Which rendition.</param>
/// <param name="Destination">Where to write it.</param>
public sealed record GetSharedImageQuery(string Token, int Width, Stream Destination);

internal sealed class GetSharedImageQueryHandler(
    IRecipeShareRepository shares,
    IRecipeRepository recipes,
    IImageStore images)
    : IQueryHandler<GetSharedImageQuery, ImageDelivery>
{
    public async Task<Result<ImageDelivery>> Handle(
        GetSharedImageQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetSharedImage");

        var share = await shares
            .FindByTokenAsync(query.Token, cancellationToken)
            .ConfigureAwait(false);

        var hash = await share.Match(
            link => recipes.ImageHashAsync(link.RecipeId, cancellationToken),
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
