using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes;
using Application.Telemetry;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Suggestions.Dismiss;

/// <summary>Hides a recipe from one person's suggestions, or stops hiding it.</summary>
public sealed record DismissSuggestionCommand(Guid RecipeId, Guid UserId, bool Hidden);

/// <summary>Records "not this": one handler for both directions so the visibility check cannot drift.</summary>
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

        // Proved like every personal write about a recipe, so hiding cannot reveal whether another's recipe exists.
        var visible = await RecipeAccess
            .VisibleHouseholdAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await visible.Match(
            _ => Write(command, cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result> Write(
        DismissSuggestionCommand command,
        CancellationToken cancellationToken)
    {
        if (!command.Hidden)
        {
            var restored = await feedback
                .RestoreAsync(command.UserId, command.RecipeId, cancellationToken)
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
            .DismissAsync(command.UserId, command.RecipeId, time.GetUtcNow(), cancellationToken)
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
