using Application.Abstractions;
using Contracts.Recipes;
using Domain.Shared;

namespace Application.Recipes;

/// <summary>
/// Puts an image on a recipe, whoever produced it.
/// </summary>
/// <remarks>
/// Extracted when a second caller appeared. An uploaded photograph and one the
/// assistant drew are the same thing by the time they get here — bytes of
/// unknown provenance that have to be decoded, re-encoded, addressed by their
/// hash and attached without orphaning whatever they displaced — and writing
/// that twice would be two places for the displaced-file rule to drift.
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

    private async Task<Result<RecipeDetail>> WriteAsync(
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
    /// Removes the file the new image displaced, unless somebody still wants it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two ways it can still be wanted. It may be the file that was just
    /// attached — storage is content-addressed, so re-uploading the same photo
    /// produces the same hash, and deleting it would delete the new image.
    /// </para>
    /// <para>
    /// Or another recipe may point at it, which the same content addressing
    /// makes possible and importing a library makes ordinary: fifty recipes
    /// carrying one placeholder are fifty rows and one file. Deleting it there
    /// would not break the recipe being edited — it would break the other
    /// forty-nine, with nothing to connect the two events.
    /// </para>
    /// </remarks>
    private async Task DeleteDisplacedAsync(
        ImageReplacement displaced,
        string currentHash,
        CancellationToken cancellationToken)
    {
        if (displaced.PreviousContentHash is not { Length: > 0 } previous || previous == currentHash)
        {
            return;
        }

        // Asked inside the caller's transaction, after the row that pointed at
        // it is gone — so the answer is about who is left.
        var wanted = await recipes
            .IsImageStillUsedAsync(previous, cancellationToken)
            .ConfigureAwait(false);

        if (!wanted)
        {
            await images.DeleteAsync(previous, cancellationToken).ConfigureAwait(false);
        }
    }
}
