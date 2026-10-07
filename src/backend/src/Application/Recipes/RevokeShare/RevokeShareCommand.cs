using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;

namespace Application.Recipes.RevokeShare;

/// <summary>Takes a recipe's link back; nothing that was sent still works.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record RevokeShareCommand(Guid RecipeId, Guid UserId);

internal sealed class RevokeShareCommandHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IRecipeShareRepository shares,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RevokeShareCommand>
{
    public async Task<Result> Handle(
        RevokeShareCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.RevokeShare");

        var editable = await RecipeAccess
            .EditableHouseholdAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await editable.Match(
            _ => unitOfWork.InTransactionAsync(
                token => shares.RemoveAsync(command.RecipeId, token),
                cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
