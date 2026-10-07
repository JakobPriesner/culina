using Application.Abstractions;
using Contracts.Recipes;
using Domain.Shared;

namespace Application.Recipes;

/// <summary>
/// Puts an image on a recipe, whoever produced it, or takes it off.
/// </summary>
/// <remarks>
/// Extracted when a second caller appeared. An uploaded photograph and one the
/// assistant drew are the same thing by the time they get here — bytes of
/// unknown provenance that have to be decoded, re-encoded, addressed by their
/// hash and attached without orphaning whatever they displaced — and writing
/// that twice would be two places for the displaced-file rule to drift.
/// Removing a picture displaces one too, so it lives here as well; when it
/// lived in its own handler, that copy of the rule did drift.
/// </remarks>
/// <param name="recipes">The recipe rows.</param>
/// <param name="images">The image store.</param>
/// <param name="unitOfWork">The transaction the row change happens in.</param>
/// <param name="time">The injected clock.</param>
public sealed class RecipeImageWriter(
    IRecipeRepository recipes,
    IImageStore images,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    private const string WebpContentType = "image/webp";

    /// <summary>Stores the bytes and points the recipe at them.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="content">The bytes. The caller still owns the stream.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task<Result<RecipeDetail>> AttachAsync(
        Guid recipeId,
        Stream content,
        CancellationToken cancellationToken)
    {
        // Written to the volume before the row is touched. A file with no row
        // is orphaned storage a sweep can reclaim; a row with no file is a
        // broken image on the page.
        var stored = await images.StoreAsync(content, cancellationToken).ConfigureAwait(false);

        return await stored.Match(
            image => WriteAsync(recipeId, image, cancellationToken),
            error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>Takes the image off a recipe.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <remarks>
    /// Here rather than in its handler, so that removing and replacing a
    /// picture share the one rule about the file left behind.
    /// </remarks>
    public async Task<Result> RemoveAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        var removed = await unitOfWork.InTransactionAsync(
            token => recipes.RemoveImageAsync(recipeId, time.GetUtcNow(), token),
            cancellationToken).ConfigureAwait(false);

        return await removed.Match(
            async displaced =>
            {
                await ReleaseAsync(displaced, currentHash: null, cancellationToken).ConfigureAwait(false);

                return Result.Success();
            },
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Result<RecipeDetail>> WriteAsync(
        Guid recipeId,
        StoredImage image,
        CancellationToken cancellationToken)
    {
        var replaced = await unitOfWork.InTransactionAsync(
            token => recipes.SetImageAsync(recipeId, image, WebpContentType, time.GetUtcNow(), token),
            cancellationToken).ConfigureAwait(false);

        return await replaced.Match(
            async displaced =>
            {
                await ReleaseAsync(displaced, image.ContentHash, cancellationToken).ConfigureAwait(false);

                var reloaded = await recipes.FindAsync(recipeId, cancellationToken).ConfigureAwait(false);

                return reloaded.Map(recipe => recipe.Describe());
            },
            error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the file a committed change displaced, unless something still
    /// wants it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// After the commit, never inside it: a file deleted for a transaction that
    /// then rolled back is a broken picture, while a file left behind by a
    /// failure here is only storage a later sweep can reclaim.
    /// </para>
    /// <para>
    /// Three ways it can still be wanted. It may be the file that was just
    /// attached — storage is content-addressed, so re-uploading the same photo
    /// produces the same hash, and deleting it would delete the new image.
    /// Another recipe may point at it, which importing a library makes
    /// ordinary: fifty recipes carrying one placeholder are fifty rows and one
    /// file. Or it may be a cook photo, anybody's, taken from the same bytes.
    /// Deleting it there would not break the recipe being edited — it would
    /// break the other one, with nothing to connect the two events.
    /// </para>
    /// </remarks>
    private async Task ReleaseAsync(
        ImageReplacement displaced,
        string? currentHash,
        CancellationToken cancellationToken)
    {
        if (displaced.PreviousContentHash is not { Length: > 0 } previous || previous == currentHash)
        {
            return;
        }

        var wanted = await recipes
            .IsImageStillUsedAsync(previous, cancellationToken)
            .ConfigureAwait(false);

        if (!wanted)
        {
            await images.DeleteAsync(previous, cancellationToken).ConfigureAwait(false);
        }
    }
}
