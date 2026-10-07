using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes.GetImage;
using Application.Telemetry;
using Domain.Shared;

namespace Application.Recipes.GetSharedImage;

/// <summary>Finds a published recipe's photograph.</summary>
/// <remarks>Separate from <see cref="GetRecipeImageQuery"/>, whose job is the membership check on every read; a switch-off flag there is what it warns against.</remarks>
public sealed record GetSharedImageQuery(string Token, int Width);

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

        return tracked.Record(hash.Map(contentHash => ImageDelivery.Of(images, contentHash, query.Width)));
    }
}
