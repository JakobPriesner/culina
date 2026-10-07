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
    RecipeImageWriter images)
    : ICommandHandler<RemoveRecipeImageCommand>
{
    public async Task<Result> Handle(
        RemoveRecipeImageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.RemoveImage");

        var editable = await RecipeAccess
            .EditableAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await editable.Match(
            _ => images.RemoveAsync(command.RecipeId, cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
