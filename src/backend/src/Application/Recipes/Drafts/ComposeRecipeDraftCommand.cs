using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Assistance;
using Application.Households;
using Application.Telemetry;
using Contracts.Streaming;
using Domain.Assistance;
using Domain.Recipes;
using Domain.Shared;
using Event = Contracts.Recipes.Drafts.Event;

namespace Application.Recipes.Drafts;

/// <summary>Asks the assistant for a recipe.</summary>
/// <param name="Kind">Which of the three: idea, text or revision.</param>
/// <param name="HouseholdId">Whose kitchen it is for.</param>
/// <param name="Material">The idea or the pasted text.</param>
/// <param name="RecipeId">Which recipe to rewrite.</param>
/// <param name="Language">The language to answer in, for the first two.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record ComposeRecipeDraftCommand(
    string Kind,
    Guid HouseholdId,
    string? Material,
    Guid? RecipeId,
    string? Language,
    Guid UserId)
{
    /// <summary>
    /// A photograph to read a recipe out of, for the <c>photo</c> kind.
    /// </summary>
    /// <remarks>
    /// Bytes rather than a stream, because they go into a request body that may
    /// be built more than once and a stream read twice is a stream read once.
    /// Not part of the positional record: every other caller has none, and a
    /// twelfth constructor argument that is almost always empty is an argument
    /// nobody reads.
    /// </remarks>
    public ReadOnlyMemory<byte> Photograph { get; init; }

    /// <summary>What kind of photograph, when there is one.</summary>
    public string? PhotographMediaType { get; init; }
}

/// <summary>
/// A draft, arriving.
/// </summary>
/// <param name="Events">
/// The recipe as it is written: thin at first, fuller each time, and a last one
/// that says it is finished or says why it stopped.
/// </param>
/// <remarks>
/// A wrapper rather than the sequence itself, so the handler's result reads the
/// same as every other handler's and the endpoint can turn a refusal into a
/// status code before a single byte of the body has been sent.
/// </remarks>
public sealed record DraftProgress(IAsyncEnumerable<Event> Events);

internal sealed class ComposeRecipeDraftCommandHandler(
    AssistantRun assistant,
    IRecipeRepository recipes,
    IHouseholdRepository households)
    : ICommandHandler<ComposeRecipeDraftCommand, DraftProgress>
{
    /// <summary>
    /// How much text this will send.
    /// </summary>
    /// <remarks>
    /// Generous enough for a long recipe pasted out of a message, short enough
    /// that a pasted book is refused before it becomes a bill. The limit is
    /// about cost rather than about capability: a model would happily read ten
    /// times this, at ten times the price, on a request anybody with an account
    /// can make.
    /// </remarks>
    private const int LongestMaterial = 20_000;

    /// <summary>
    /// Everything that can refuse the ask, then the stream.
    /// </summary>
    /// <remarks>
    /// The split matters more here than in a handler that answers once. Access,
    /// the material, the capability and the budget are all decided before
    /// anything is returned, so each of them is still an ordinary failure with
    /// an ordinary status code. After that the response has begun and the only
    /// way left to say anything is on the stream itself.
    /// </remarks>
    public async Task<Result<DraftProgress>> Handle(
        ComposeRecipeDraftCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

#pragma warning disable CA2000 // Handed to the stream below, which closes it.
        // Not disposed here, unlike every other handler's: a stream that has
        // been handed back has not finished doing the thing being measured.
        var tracked = UseCaseActivity.Start("Recipes.ComposeDraft");
#pragma warning restore CA2000

        var opened = await OpenAsync(command, cancellationToken).ConfigureAwait(false);

        return opened.Match(
            parts => Result<DraftProgress>.Success(new DraftProgress(Watched(parts, tracked))),
            error => Refused(tracked, error));
    }

    private static Result<DraftProgress> Refused(UseCaseActivity tracked, Error error)
    {
        using (tracked)
        {
            return tracked.Record(Result<DraftProgress>.Failure(error));
        }
    }

    private async Task<Result<IAsyncEnumerable<Composing>>> OpenAsync(
        ComposeRecipeDraftCommand command,
        CancellationToken cancellationToken)
    {
        var permitted = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        return await permitted.Match(
            () => AskAsync(command, cancellationToken),
            error => Task.FromResult(Result<IAsyncEnumerable<Composing>>.Failure(error)))
            .ConfigureAwait(false);
    }

    private async Task<Result<IAsyncEnumerable<Composing>>> AskAsync(
        ComposeRecipeDraftCommand command,
        CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(command, cancellationToken).ConfigureAwait(false);

        return await prepared.Match(
            request => assistant.ComposeStreamAsync(
                new Asker(command.UserId, command.HouseholdId),
                request,
                cancellationToken),
            error => Task.FromResult(Result<IAsyncEnumerable<Composing>>.Failure(error)))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Works out what to ask for, and what to ask it about.
    /// </summary>
    /// <remarks>
    /// The instruction comes from <see cref="AssistantPrompts"/> and the
    /// material from the request or from a stored recipe — never joined. That
    /// separation is structural rather than a convention, because on a shared
    /// instance the material is whatever somebody else typed.
    /// </remarks>
    private async Task<Result<Composition>> PrepareAsync(
        ComposeRecipeDraftCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Kind == "revision")
        {
            return await ForRevisionAsync(command, cancellationToken).ConfigureAwait(false);
        }

        var photographed = command.Kind == "photo";
        var material = Material(command);

        // A photograph on its own is enough; words on their own are enough;
        // neither is not.
        if (material is null && !photographed)
        {
            return AssistanceErrors.NothingToWorkFrom;
        }

        if (material is { Length: > LongestMaterial })
        {
            return AssistanceErrors.TooMuchToWorkFrom;
        }

        if (photographed && command.Photograph.IsEmpty)
        {
            return AssistanceErrors.NothingToWorkFrom;
        }

        return RecipeWords.ToLanguage(command.Language ?? "en").Map(language =>
        {
            var writing = command.Kind == "idea";

            return new Composition
            {
                Capability = writing ? Capability.Draft : Capability.Read,
                Language = language,
                Instruction = writing
                    ? AssistantPrompts.Draft(language)
                    : AssistantPrompts.Read(language),
                Material = material,
                Picture = command.Photograph,
                PictureMediaType = command.PhotographMediaType
            };
        });
    }

    private async Task<Result<Composition>> ForRevisionAsync(
        ComposeRecipeDraftCommand command,
        CancellationToken cancellationToken)
    {
        if (command.RecipeId is not { } recipeId)
        {
            return AssistanceErrors.NothingToWorkFrom;
        }

        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, recipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        return visible.Map(recipe => new Composition
        {
            Capability = Capability.Improve,
            // What it is stored as, which is the caller's business and not the
            // prompt's. The instruction names no language at all: rewriting a
            // German recipe into English is not tidying it up, and the stored
            // field is not evidence of which one it is — nothing has ever asked
            // anybody to set it, so a German recipe is usually stored as "en".
            Language = recipe.Language,
            Instruction = AssistantPrompts.Improve(),
            Material = RecipeAsText.Of(recipe)
        });
    }

    /// <summary>
    /// The stream a client reads, with the span held open across it.
    /// </summary>
    /// <remarks>
    /// One draft id for every event of one ask, because they are all the same
    /// draft arriving — a new id per event would be a client unable to tell a
    /// second ask from the next few characters of the first.
    /// </remarks>
    private static async IAsyncEnumerable<Event> Watched(
        IAsyncEnumerable<Composing> parts,
        UseCaseActivity tracked)
    {
        using (tracked)
        {
            var draftId = Guid.CreateVersion7();

            await foreach (var part in parts.ConfigureAwait(false))
            {
                var failure = Wrong(part);

                if (failure is not null)
                {
                    tracked.Record(Result.Failure(failure));
                }

                yield return new Event
                {
                    Draft = part.Recipe.ToResponse(draftId),
                    Finished = part.Finished,
                    Problem = failure is null
                        ? null
                        : new Problem { Code = failure.Code, Detail = failure.Description }
                };
            }
        }
    }

    /// <summary>
    /// What went wrong with this part, if anything did.
    /// </summary>
    /// <remarks>
    /// An answer with nothing in it counts. A model that finished having said
    /// no title, no ingredients and no steps did not write a thin recipe, it
    /// failed to answer — and a blank draft offered for correction is worse
    /// than being told to ask again.
    /// </remarks>
    private static Error? Wrong(Composing part) => part switch
    {
        { Failure: { } failure } => failure,
        { Finished: true } when !part.Recipe.IsUsable() => AssistanceErrors.UnusableAnswer,
        _ => null
    };

    private static string? Material(ComposeRecipeDraftCommand command) =>
        string.IsNullOrWhiteSpace(command.Material) ? null : command.Material.Trim();
}
