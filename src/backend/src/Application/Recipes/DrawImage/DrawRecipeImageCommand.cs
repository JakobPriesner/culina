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

/// <summary>
/// A picture being drawn.
/// </summary>
/// <param name="Events">
/// A tick every few seconds while it is being drawn, then one carrying the
/// recipe with its new picture — or saying why there is none.
/// </param>
public sealed record DrawingProgress(IAsyncEnumerable<DrawingEvent> Events);

internal sealed class DrawRecipeImageCommandHandler(
    AssistantRun assistant,
    IRecipeRepository recipes,
    IHouseholdRepository households,
    RecipeImageWriter images,
    TimeProvider time)
    : ICommandHandler<DrawRecipeImageCommand, DrawingProgress>
{
    /// <summary>
    /// How often the stream says it is still going.
    /// </summary>
    /// <remarks>
    /// Often enough that nobody decides it has broken, and far inside the
    /// interval any proxy would close an idle connection at. It buys nothing
    /// from the provider — the drawing takes what it takes — and it is the
    /// difference between a wait somebody sits through and one they reload the
    /// page in the middle of.
    /// </remarks>
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(3);

    public async Task<Result<DrawingProgress>> Handle(
        DrawRecipeImageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

#pragma warning disable CA2000 // Handed to the stream below, which closes it.
        // Not disposed here: the work this measures has not happened yet, and
        // the stream closes it when it has.
        var tracked = UseCaseActivity.Start("Recipes.DrawImage");
#pragma warning restore CA2000

        var ready = await OpenAsync(command, cancellationToken).ConfigureAwait(false);

        return ready.Match(
            afforded => Result<DrawingProgress>.Success(
                new DrawingProgress(
                    Drawing(command, afforded.Recipe, afforded.Provider, tracked, cancellationToken))),
            error => Refused(tracked, error));
    }

    /// <summary>
    /// Everything that can refuse this, before a byte of the answer goes out.
    /// </summary>
    /// <remarks>
    /// The recipe has to be visible, the assistant has to be switched on for
    /// drawing, the provider has to be one that draws at all, and the month has
    /// to have budget left. Every one of those is a status code — a 404, a 400,
    /// a 429 with how long to wait on it — and none of them can be once the
    /// stream has opened with a 200.
    /// </remarks>
    private async Task<Result<Afforded>> OpenAsync(
        DrawRecipeImageCommand command,
        CancellationToken cancellationToken)
    {
        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, command.RecipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        return await visible.Match(
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
    /// Draws, saying how long it has been drawing for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The drawing is started as a task and then raced against a timer, rather
    /// than awaited. There is nothing partial to send from a provider making a
    /// picture — it hands over a finished image or none — so what the stream
    /// carries until the end is the one fact worth having: that this is still
    /// happening, and for how long.
    /// </para>
    /// <para>
    /// Unlike the ordinary checks, everything that can go wrong from here is
    /// said on the stream. The status line went out with the first tick.
    /// </para>
    /// </remarks>
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

            // Straight away, before the first tick. It sends the headers, which
            // is what turns "the request is hanging" into "the work started".
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
    /// <remarks>
    /// The timer is cancelled the moment it is no longer wanted, rather than
    /// left to run out. A call every three seconds for two minutes would
    /// otherwise leave a trail of timers nobody is waiting for.
    /// </remarks>
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
