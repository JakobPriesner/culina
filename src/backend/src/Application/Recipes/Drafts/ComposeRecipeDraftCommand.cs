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
    /// <summary>A photograph to read a recipe out of, for the <c>photo</c> kind.</summary>
    /// <remarks>Bytes, not a stream: the request body may be built more than once.</remarks>
    public ReadOnlyMemory<byte> Photograph { get; init; }

    /// <summary>More pages of the same recipe.</summary>
    public IReadOnlyList<RecipePicture> Pictures { get; init; } = [];

    /// <summary>Speech captions from the source video.</summary>
    public string? Transcript { get; init; }
}

/// <summary>A draft, arriving.</summary>
/// <param name="Events">
/// The recipe as written: thin at first, fuller each time, last one finished or says why it
/// stopped.
/// </param>
/// <remarks>A wrapper so a refusal can become a status code before any body byte is sent.</remarks>
public sealed record DraftProgress(IAsyncEnumerable<Event> Events);

internal sealed class ComposeRecipeDraftCommandHandler(
    AssistantRun assistant,
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IImageStore images)
    : ICommandHandler<ComposeRecipeDraftCommand, DraftProgress>
{
    /// <summary>
    /// Cap on material length: about cost, not capability, so a pasted book is refused.
    /// </summary>
    private const int LongestMaterial = DraftLimits.MaxMaterialCharacters;

    /// <summary>Everything that can refuse the ask, then the stream.</summary>
    /// <remarks>
    /// Refusals are decided first so they stay ordinary status codes; afterwards only the stream
    /// can speak.
    /// </remarks>
    public async Task<Result<DraftProgress>> Handle(
        ComposeRecipeDraftCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

#pragma warning disable CA2000 // Handed to the stream below, which closes it.
        // Not disposed here: a stream handed back has not finished what is being measured.
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

    /// <summary>Works out what to ask for and about.</summary>
    /// <remarks>
    /// Instruction and material are never joined: on a shared instance the material is someone
    /// else's text.
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
        var social = command.Kind == "social";
        var material = Material(command);

        // A photograph or words alone are enough; neither is not.
        if (material is null && !photographed && command.Pictures.Count == 0
            && string.IsNullOrWhiteSpace(command.Transcript))
        {
            return AssistanceErrors.NothingToWorkFrom;
        }

        if ((material?.Length ?? 0) + (command.Transcript?.Length ?? 0) > LongestMaterial)
        {
            return AssistanceErrors.TooMuchToWorkFrom;
        }

        if (photographed && command.Photograph.IsEmpty)
        {
            return AssistanceErrors.NothingToWorkFrom;
        }

        var pictures = await ReEncodedAsync(command, cancellationToken).ConfigureAwait(false);

        return pictures.Bind(readable => RecipeWords.ToLanguage(command.Language ?? "en").Map(language =>
        {
            var writing = command.Kind == "idea";

            return new Composition
            {
                Capability = writing ? Capability.Draft : Capability.Read,
                Language = language,
                Instruction = writing
                    ? AssistantPrompts.Draft(language)
                    : social ? AssistantPrompts.Social(language) : AssistantPrompts.Read(language),
                Material = social ? AssistantPrompts.SocialMaterial(material, command.Transcript) : material,
                Pictures = readable
            };
        }));
    }

    /// <summary>Every picture the provider will see, photograph first, re-encoded.</summary>
    /// <remarks>
    /// Done here because every picture route ends at this handler; a missed one would leak photo
    /// metadata such as GPS.
    /// </remarks>
    private async Task<Result<IReadOnlyList<RecipePicture>>> ReEncodedAsync(
        ComposeRecipeDraftCommand command,
        CancellationToken cancellationToken)
    {
        var originals = command.Pictures
            .Select(picture => picture.Content)
            .Prepend(command.Photograph)
            .Where(content => !content.IsEmpty);

        List<Result<RecipePicture>> encoded = [];

        foreach (var original in originals)
        {
            encoded.Add(await images.ReEncodeForReadingAsync(original, cancellationToken).ConfigureAwait(false));
        }

        return encoded.Collect();
    }

    private async Task<Result<Composition>> ForRevisionAsync(
        ComposeRecipeDraftCommand command,
        CancellationToken cancellationToken)
    {
        if (command.RecipeId is not { } recipeId)
        {
            return AssistanceErrors.NothingToWorkFrom;
        }

        var editable = await RecipeAccess
            .EditableAsync(recipes, households, recipeId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Editable is not enough: the call is billed to the request's household, so the recipe must
        // be its own.
        var owned = editable.Bind(recipe => recipe.HouseholdId == command.HouseholdId
            ? Result<Recipe>.Success(recipe)
            : RecipeErrors.NotFound(recipeId));

        return owned.Map(recipe => new Composition
        {
            Capability = Capability.Improve,
            // The stored language, which is the caller's business: the instruction names none, and
            // the field is unreliable.
            Language = recipe.Language,
            Instruction = AssistantPrompts.Improve(),
            Material = RecipeAsText.Of(recipe)
        });
    }

    /// <summary>
    /// The stream a client reads, with the span held open across it; one draft id for all events of
    /// one ask.
    /// </summary>
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
    /// What went wrong with this part, if anything; a finished answer with nothing in it is a
    /// failure.
    /// </summary>
    private static Error? Wrong(Composing part) => part switch
    {
        { Failure: { } failure } => failure,
        { Finished: true } when !part.Recipe.IsUsable() => AssistanceErrors.UnusableAnswer,
        _ => null
    };

    private static string? Material(ComposeRecipeDraftCommand command) =>
        string.IsNullOrWhiteSpace(command.Material) ? null : command.Material.Trim();
}
