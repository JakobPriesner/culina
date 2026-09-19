using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Assistance;
using Application.Recipes.SetImage;
using Application.Telemetry;
using Contracts.Recipes;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.DrawImage;

/// <summary>Draws a picture for a recipe that has none.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record DrawRecipeImageCommand(Guid RecipeId, Guid UserId);

internal sealed class DrawRecipeImageCommandHandler(
    AssistantRun assistant,
    IRecipeRepository recipes,
    IHouseholdRepository households,
    RecipeImageWriter images)
    : ICommandHandler<DrawRecipeImageCommand, RecipeDetail>
{
    public async Task<Result<RecipeDetail>> Handle(
        DrawRecipeImageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.DrawImage");

        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await visible.Match(
            recipe => DrawAsync(command, recipe, cancellationToken),
            error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<RecipeDetail>> DrawAsync(
        DrawRecipeImageCommand command,
        Recipe recipe,
        CancellationToken cancellationToken)
    {
        var drawn = await assistant
            .DrawAsync(
                new Asker(command.UserId, recipe.HouseholdId),
                new Drawing
                {
                    Subject = AssistantPrompts.Draw(recipe.Title.Value, recipe.Description)
                },
                cancellationToken)
            .ConfigureAwait(false);

        return await drawn.Match(
            async picture =>
            {
                // Disposed here rather than by the store, which is handed a
                // stream it does not own — the same contract an upload has.
                using (picture)
                {
                    return await images
                        .AttachAsync(command.RecipeId, picture.Picture, cancellationToken)
                        .ConfigureAwait(false);
                }
            },
            error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);
    }
}
