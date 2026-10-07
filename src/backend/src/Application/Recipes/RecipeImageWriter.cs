using Application.Abstractions;
using Contracts.Recipes;
using Domain.Shared;

namespace Application.Recipes;

/// <summary>Attaches or removes a recipe image, owning the rule for the file it displaces.</summary>
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
        // File first, then the row: an orphaned file is reclaimable, a row without a file is a broken image.
        var stored = await images.StoreAsync(content, cancellationToken).ConfigureAwait(false);

        return await stored.Match(
            image => WriteAsync(recipeId, image, cancellationToken),
            error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>Takes the image off a recipe.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
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

    // Runs after the commit, never inside it: a file deleted for a rolled-back transaction is a broken
    // picture. Storage is content-addressed, so the file may still be wanted: it is the one just attached,
    // another recipe shares it, or a cook photo uses the same bytes.
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
