using System.Runtime.CompilerServices;
using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Assistance;
using Application.Recipes.SetImage;
using Application.Telemetry;
using Contracts.Recipes;
using Contracts.Streaming;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.DrawImage;

/// <summary>Draws a picture for a recipe that has none.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record DrawRecipeImageCommand(Guid RecipeId, Guid UserId);

/// <summary>A picture being drawn.</summary>
/// <param name="Events">A tick every few seconds, then one with the recipe and its new picture or the reason there is none.</param>
public sealed record DrawingProgress(IAsyncEnumerable<DrawingEvent> Events);

internal sealed class DrawRecipeImageCommandHandler(
    AssistantRun assistant,
    IRecipeRepository recipes,
    IHouseholdRepository households,
    RecipeImageWriter images,
    TimeProvider time)
    : ICommandHandler<DrawRecipeImageCommand, DrawingProgress>
{
    /// <summary>How often the stream says it is still going; keeps users and proxies from giving up.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(3);

    public async Task<Result<DrawingProgress>> Handle(
        DrawRecipeImageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

#pragma warning disable CA2000 // Handed to the stream below, which closes it.
        var tracked = UseCaseActivity.Start("Recipes.DrawImage");
#pragma warning restore CA2000

        var ready = await OpenAsync(command, cancellationToken).ConfigureAwait(false);

        return ready.Match(
            afforded => Result<DrawingProgress>.Success(
                new DrawingProgress(
                    Drawing(command, afforded.Recipe, afforded.Provider, tracked, cancellationToken))),
            error => Refused(tracked, error));
    }

    /// <summary>Every check that must refuse with a status code, before the stream opens with a 200.</summary>
    private async Task<Result<Afforded>> OpenAsync(
        DrawRecipeImageCommand command,
        CancellationToken cancellationToken)
    {
        var editable = await RecipeAccess
            .EditableAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        return await editable.Match(
            async recipe =>
            {
                var reserved = await assistant
                    .ReserveDrawingAsync(new Asker(command.UserId, recipe.HouseholdId), cancellationToken)
                    .ConfigureAwait(false);

                return reserved.Map(provider => new Afforded(recipe, provider));
            },
            error => Task.FromResult(Result<Afforded>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>A recipe to draw, and a provider already paid for.</summary>
    private sealed record Afforded(Recipe Recipe, AssistantRun.ReservedDrawing Provider);

    private static Result<DrawingProgress> Refused(UseCaseActivity tracked, Error error)
    {
        using (tracked)
        {
            return tracked.Record(Result<DrawingProgress>.Failure(error));
        }
    }

    /// <summary>
    /// Races the drawing against a tick timer; the provider returns a finished image or none, so progress is only elapsed time.
    /// Errors from here on are sent on the stream, as the status line is already out.
    /// </summary>
    private async IAsyncEnumerable<DrawingEvent> Drawing(
        DrawRecipeImageCommand command,
        Recipe recipe,
        AssistantRun.ReservedDrawing provider,
        UseCaseActivity tracked,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using (tracked)
        {
            var startedAt = time.GetUtcNow();
            var drawing = DrawAsync(command, recipe, provider, cancellationToken);

            // Sent straight away: the headers turn "hanging" into "started".
            yield return new DrawingEvent { Seconds = 0 };

            while (await StillDrawingAsync(drawing, cancellationToken).ConfigureAwait(false))
            {
                yield return new DrawingEvent { Seconds = Since(startedAt) };
            }

            var drawn = await drawing.ConfigureAwait(false);

            tracked.Record(drawn);

            yield return drawn.Match(
                detail => new DrawingEvent
                {
                    Seconds = Since(startedAt),
                    Finished = true,
                    Recipe = detail
                },
                error => new DrawingEvent
                {
                    Seconds = Since(startedAt),
                    Finished = true,
                    Problem = new Problem { Code = error.Code, Detail = error.Description }
                });
        }
    }

    /// <summary>Waits one tick, and says whether the drawing is still going.</summary>
    /// <summary>Waits one tick, and says whether the drawing is still going. Cancels the timer once unneeded.</summary>
    private async Task<bool> StillDrawingAsync(
        Task<Result<RecipeDetail>> drawing,
        CancellationToken cancellationToken)
    {
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var ticked = Task.Delay(Tick, time, waiting.Token);

        if (await Task.WhenAny(drawing, ticked).ConfigureAwait(false) == ticked)
        {
            return true;
        }

        await waiting.CancelAsync().ConfigureAwait(false);

        return false;
    }

    private int Since(DateTimeOffset startedAt) =>
        (int)(time.GetUtcNow() - startedAt).TotalSeconds;

    private async Task<Result<RecipeDetail>> DrawAsync(
        DrawRecipeImageCommand command,
        Recipe recipe,
        AssistantRun.ReservedDrawing provider,
        CancellationToken cancellationToken)
    {
        var drawn = await provider
            .AskAsync(
                new Drawing
                {
                    Subject = AssistantPrompts.Draw(
                        recipe.Title.Value,
                        recipe.Description,
                        recipe.Groups,
                        recipe.Steps)
                },
                cancellationToken)
            .ConfigureAwait(false);

        return await drawn.Match(
            async picture =>
            {
                // Disposed here: the store does not own the stream it is handed.
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
