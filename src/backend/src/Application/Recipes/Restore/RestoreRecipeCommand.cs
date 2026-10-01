using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.Restore;

/// <summary>Takes a recipe out of its household's bin.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking; any member of the household may.</param>
public sealed record RestoreRecipeCommand(Guid RecipeId, Guid UserId);

internal sealed class RestoreRecipeCommandHandler(
    ITrashRepository trash,
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RestoreRecipeCommand>
{
    public async Task<Result> Handle(RestoreRecipeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.Restore");

        var householdId = await trash
            .HouseholdOfDeletedRecipeAsync(command.RecipeId, cancellationToken)
            .ConfigureAwait(false);

        // The same people who could delete it — the household's own members,
        // not those of a household inheriting from it. Anybody else is told
        // there is nothing to restore, as they are told there is no recipe.
        var permitted = householdId is { } id
            && await households.IsMemberAsync(id, command.UserId, cancellationToken).ConfigureAwait(false);

        Result result = permitted
            ? await unitOfWork.InTransactionAsync(
                async token => await trash.RestoreRecipeAsync(command.RecipeId, token).ConfigureAwait(false)
                    ? Result.Success()
                    : Result.Failure(RecipeErrors.NotFound(command.RecipeId)),
                cancellationToken).ConfigureAwait(false)
            : RecipeErrors.NotFound(command.RecipeId);

        return tracked.Record(result);
    }
}
