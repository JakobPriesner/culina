using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;

namespace Application.Recipes.Delete;

/// <summary>Puts a recipe in its household's bin, from which it can be restored.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="ExpectedVersion">The version the caller was holding.</param>
public sealed record DeleteRecipeCommand(Guid RecipeId, Guid UserId, long ExpectedVersion);

internal sealed class DeleteRecipeCommandHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<DeleteRecipeCommand>
{
    public async Task<Result> Handle(
        DeleteRecipeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.Delete");

        var found = await RecipeAccess
            .EditableAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            recipe => unitOfWork.InTransactionAsync(
                token => recipes.DeleteAsync(
                    recipe.Id, command.ExpectedVersion, command.UserId, time.GetUtcNow(), token),
                cancellationToken),
            error => AlreadyGoneAsync(command, error, cancellationToken)).ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>
    /// Deleting something already gone is the outcome the caller wanted, so a second DELETE
    /// succeeds. A recipe the caller can still read, only not change, is not gone: it stays
    /// not-found, as it is for every other change.
    /// </summary>
    private async Task<Result> AlreadyGoneAsync(
        DeleteRecipeCommand command,
        Error error,
        CancellationToken cancellationToken)
    {
        var visible = await RecipeAccess
            .VisibleHouseholdAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        return visible.Match(_ => Result.Failure(error), _ => Result.Success());
    }
}
