using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes;
using Domain.Shared;

namespace Application.Recipes.SetImage;

/// <summary>Attaches an uploaded image to a recipe.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="Content">The uploaded bytes.</param>
public sealed record SetRecipeImageCommand(Guid RecipeId, Guid UserId, Stream Content);

internal sealed class SetRecipeImageCommandHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IImageStore images,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<SetRecipeImageCommand, RecipeDetail>
{
    private const string WebpContentType = "image/webp";

    public async Task<Result<RecipeDetail>> Handle(
        SetRecipeImageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.SetImage");

        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await visible.Match(
            _ => StoreAsync(command, cancellationToken),
            error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<RecipeDetail>> StoreAsync(
        SetRecipeImageCommand command,
        CancellationToken cancellationToken)
    {
        // Written to the volume before the row is touched. A file with no row
        // is orphaned storage a sweep can reclaim; a row with no file is a
        // broken image on the page.
        var stored = await images.StoreAsync(command.Content, cancellationToken).ConfigureAwait(false);

        return await stored.Match(
            image => AttachAsync(command.RecipeId, image, cancellationToken),
            error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Result<RecipeDetail>> AttachAsync(
        Guid recipeId,
        StoredImage image,
        CancellationToken cancellationToken) =>
        await unitOfWork.InTransactionAsync(
            async token =>
            {
                var now = time.GetUtcNow();

                var replaced = await recipes
                    .SetImageAsync(recipeId, image, WebpContentType, now, token)
                    .ConfigureAwait(false);

                return await replaced.Match(
                    async displaced =>
                    {
                        await DeleteDisplacedAsync(displaced, image.ContentHash, token)
                            .ConfigureAwait(false);

                        var reloaded = await recipes.FindAsync(recipeId, token).ConfigureAwait(false);

                        return reloaded.Map(recipe => recipe.Describe());
                    },
                    error => Task.FromResult(Result<RecipeDetail>.Failure(error)))
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Removes the file the new image displaced, unless it is the same file.
    /// </summary>
    /// <remarks>
    /// Storage is content-addressed, so re-uploading the same photo produces
    /// the same hash — deleting it would delete the image that was just
    /// attached.
    /// </remarks>
    private async Task DeleteDisplacedAsync(
        ImageReplacement displaced,
        string currentHash,
        CancellationToken cancellationToken)
    {
        if (displaced.PreviousContentHash is { Length: > 0 } previous && previous != currentHash)
        {
            await images.DeleteAsync(previous, cancellationToken).ConfigureAwait(false);
        }
    }
}
