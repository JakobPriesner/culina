using Application.Abstractions;
using Domain.Shared;

namespace Application.Recipes;

/// <summary>Deletes an image file once nothing points at it, safely against an upload of the same bytes.</summary>
/// <remarks>
/// Storage is content-addressed, so an identical upload reuses the file already there. The check for
/// "still used" and the delete happen under the lock that attaching holds around saving its row, so the
/// file cannot be deleted between an upload finding it and its row being committed.
/// </remarks>
/// <param name="recipes">The rows that may still point at the file.</param>
/// <param name="images">The image store.</param>
/// <param name="unitOfWork">The transaction the lock lives in.</param>
public sealed class ImageRelease(
    IRecipeRepository recipes,
    IImageStore images,
    IUnitOfWork unitOfWork)
{
    /// <summary>Deletes the file unless a recipe picture or cook photo still uses it.</summary>
    /// <param name="contentHash">Which image.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <remarks>Run after the commit that dropped the last reference, never inside it.</remarks>
    public Task<Result> ReleaseIfUnusedAsync(string contentHash, CancellationToken cancellationToken) =>
        unitOfWork.InTransactionAsync(
            async token =>
            {
                await recipes.LockImageAsync(contentHash, token).ConfigureAwait(false);

                var wanted = await recipes.IsImageStillUsedAsync(contentHash, token).ConfigureAwait(false);

                return wanted
                    ? Result.Success()
                    : await images.DeleteAsync(contentHash, token).ConfigureAwait(false);
            },
            cancellationToken);
}
