using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;

namespace Application.Recipes.RemoveImage;

/// <summary>Removes a recipe's image.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record RemoveRecipeImageCommand(Guid RecipeId, Guid UserId);

internal sealed class RemoveRecipeImageCommandHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IImageStore images,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<RemoveRecipeImageCommand>
{
    public async Task<Result> Handle(
        RemoveRecipeImageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.RemoveImage");

        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await visible.Match(
            _ => unitOfWork.InTransactionAsync(
                async token =>
                {
                    var removed = await recipes
                        .RemoveImageAsync(command.RecipeId, time.GetUtcNow(), token)
                        .ConfigureAwait(false);

                    return await removed.Match(
                        async displaced =>
                        {
                            // Only when nothing else points at it. Content-
                            // addressed storage means one file can serve many
                            // recipes, and an imported library where fifty
                            // carry the same placeholder makes that ordinary
                            // rather than a curiosity.
                            if (displaced.PreviousContentHash is { Length: > 0 } previous
                                && !await recipes.IsImageStillUsedAsync(previous, token)
                                    .ConfigureAwait(false))
                            {
                                await images.DeleteAsync(previous, token).ConfigureAwait(false);
                            }

                            return Result.Success();
                        },
                        error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);
                },
                cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
