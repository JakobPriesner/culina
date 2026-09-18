using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Telemetry;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Suggestions.Dismiss;

/// <summary>Hides a recipe from one person's suggestions, or stops hiding it.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Whose suggestions.</param>
/// <param name="Hidden">True to hide it, false to take that back.</param>
public sealed record DismissSuggestionCommand(Guid RecipeId, Guid UserId, bool Hidden);

/// <summary>
/// Records "not this".
/// </summary>
/// <remarks>
/// One handler for both directions because they are one fact at one address
/// with two possible values, and a pair of handlers would be two places for the
/// visibility check to drift apart.
/// </remarks>
internal sealed class DismissSuggestionCommandHandler(
    ISuggestionFeedback feedback,
    IRecipeRepository recipes,
    IHouseholdRepository households,
    TimeProvider time)
    : ICommandHandler<DismissSuggestionCommand>
{
    public async Task<Result> Handle(
        DismissSuggestionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Suggestions.Dismiss");

        // Proved the same way every other personal write about a recipe proves
        // it, so hiding one cannot be used to find out whether somebody else's
        // recipe exists.
        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await visible.Match(
            recipe => Write(recipe, command, cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result> Write(
        Recipe recipe,
        DismissSuggestionCommand command,
        CancellationToken cancellationToken)
    {
        if (!command.Hidden)
        {
            var restored = await feedback
                .RestoreAsync(command.UserId, recipe.Id, cancellationToken)
                .ConfigureAwait(false);

            return restored.Match(
                () =>
                {
                    SuggestionMetrics.Restored();

                    return Result.Success();
                },
                Result.Failure);
        }

        var dismissed = await feedback
            .DismissAsync(command.UserId, recipe.Id, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        return dismissed.Match(
            () =>
            {
                SuggestionMetrics.Dismissed();

                return Result.Success();
            },
            Result.Failure);
    }
}
