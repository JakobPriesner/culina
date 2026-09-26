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
    RecipeImageWriter images)
    : ICommandHandler<SetRecipeImageCommand, RecipeDetail>
{
    public async Task<Result<RecipeDetail>> Handle(
        SetRecipeImageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.SetImage");

        var editable = await RecipeAccess
            .EditableAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await editable.Match(
            _ => images.AttachAsync(command.RecipeId, command.Content, cancellationToken),
            error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
