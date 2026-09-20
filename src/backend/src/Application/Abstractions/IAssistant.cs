using Domain.Assistance;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>
/// One model provider, as the rest of the app needs it.
/// </summary>
/// <remarks>
/// <para>
/// Two methods for four capabilities, and that is the point. Drafting a recipe
/// from an idea, improving one somebody wrote, and reading one out of a
/// photograph are the same call — words and maybe a picture in, one structured
/// recipe out. What differs between them is the instruction, and an instruction
/// is this application's business rather than the provider's. Four methods here
/// would be the same two adapters written twice to express a difference that
/// lives one layer up.
/// </para>
/// <para>
/// Drawing is the one that is genuinely different work, so it is the one that
/// is genuinely a second method.
/// </para>
/// <para>
/// Every call is told which connection and which model to use rather than
/// reading them from the settings. An adapter that reached into the settings
/// could serve exactly one connection, which is what stopped this instance
/// having a cheap model for tidying wording and a good one for reading a
/// photograph.
/// </para>
/// </remarks>
public interface IAssistant
{
    /// <summary>Which provider this talks to.</summary>
    AssistantKind Kind { get; }

    /// <summary>Asks for a recipe.</summary>
    /// <param name="using">Where to reach the provider, and which model.</param>
    /// <param name="request">What to do, and what to do it to.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task<Result<Composed>> ComposeAsync(
        Connected @using,
        Composition request,
        CancellationToken cancellationToken);

    /// <summary>Asks for a picture.</summary>
    /// <param name="using">Where to reach the provider, and which model.</param>
    /// <param name="request">What to draw.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task<Result<Drawn>> DrawAsync(
        Connected @using,
        Drawing request,
        CancellationToken cancellationToken);

    /// <summary>
    /// What this provider currently offers.
    /// </summary>
    /// <param name="using">Where to reach it. The model on it is ignored.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <remarks>
    /// So that choosing a model is choosing from a list rather than typing a
    /// name correctly. The alternative — a text box — asks an administrator to
    /// know what their provider released this month, and silently does nothing
    /// useful when they get a character wrong.
    /// </remarks>
    Task<Result<IReadOnlyList<ModelInfo>>> ListModelsAsync(
        Connected @using,
        CancellationToken cancellationToken);
}

/// <summary>One model a provider offers.</summary>
/// <param name="Id">What to send as the model name.</param>
/// <param name="Label">What to show, where the provider says something nicer.</param>
/// <param name="CanDraw">
/// Whether it makes pictures.
/// </param>
/// <param name="Added">
/// When the provider published it, where the provider says.
/// </param>
/// <remarks>
/// <see cref="CanDraw"/> is the adapter's best reading rather than a fact the
/// providers state plainly — none of the three has a field that says "this one
/// draws". It decides which list a job's picker offers, and being wrong about
/// it costs a failed call and a clear error, not a wrong recipe.
/// </remarks>
/// <remarks>
/// <see cref="Added"/> is null for a provider that does not date its catalogue.
/// Google is one: its listing carries a name, a display name, limits and
/// capabilities, and nothing about when the model appeared. A date invented
/// here would sort a list convincingly and wrongly, so the absence is carried
/// as an absence and those listings stay in name order.
/// </remarks>
public sealed record ModelInfo(
    string Id,
    string Label,
    bool CanDraw,
    DateTimeOffset? Added = null);

/// <summary>
/// One provider, ready to be called.
/// </summary>
/// <remarks>
/// Resolved before it gets here: the key is already decrypted, the address is
/// already either the override or the provider's own, and the model is already
/// either the chosen one or the current default. Adapters therefore know
/// nothing about encryption, about settings, or about what a default is — they
/// know how to talk to one provider.
/// </remarks>
/// <param name="ApiKey">The credential, in the clear. Empty where none is needed.</param>
/// <param name="BaseUrl">Where to send the request.</param>
/// <param name="Model">Which model to ask.</param>
public sealed record Connected(string ApiKey, string BaseUrl, string Model);

/// <summary>
/// A request for a recipe, with the instruction kept apart from the material.
/// </summary>
/// <remarks>
/// The separation is the whole defence against prompt injection, so it is
/// structural rather than a convention. <see cref="Instruction"/> is written by
/// this application. <see cref="Material"/> and <see cref="Picture"/> are
/// whatever a person pasted, typed or photographed, which on a shared instance
/// means whatever somebody else pasted, typed or photographed. An adapter puts
/// them in different parts of the request and never concatenates them, and what
/// comes back is only ever read as data.
/// </remarks>
public sealed record Composition
{
    /// <summary>Which capability this is, for the ledger.</summary>
    public required Capability Capability { get; init; }

    /// <summary>The language the answer must be written in.</summary>
    public required Language Language { get; init; }

    /// <summary>What to do. Written here, never by a caller of the API.</summary>
    public required string Instruction { get; init; }

    /// <summary>What to do it to. Untrusted.</summary>
    public string? Material { get; init; }

    /// <summary>A photograph to read a recipe out of. Untrusted.</summary>
    public ReadOnlyMemory<byte> Picture { get; init; }

    /// <summary>What kind of picture, when there is one.</summary>
    public string? PictureMediaType { get; init; }
}

/// <summary>What the model said, and what it cost.</summary>
/// <param name="Recipe">The answer, still unvalidated.</param>
/// <param name="Usage">What to write in the ledger.</param>
public sealed record Composed(DraftedRecipe Recipe, ModelUsage Usage);

/// <summary>A request for a picture.</summary>
public sealed record Drawing
{
    /// <summary>What the dish is, in a sentence. Built here from the recipe.</summary>
    public required string Subject { get; init; }
}

/// <summary>A picture, and what it cost.</summary>
/// <param name="Picture">The bytes, for <see cref="IImageStore"/> to decide about.</param>
/// <param name="Usage">What to write in the ledger.</param>
public sealed record Drawn(Stream Picture, ModelUsage Usage) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => Picture.Dispose();
}

/// <summary>
/// What one call consumed.
/// </summary>
/// <remarks>
/// Taken from the provider's own answer rather than counted here. A token count
/// this app estimated would be a number that disagrees with the bill, and a
/// number that disagrees with the bill is worse than no number.
/// </remarks>
/// <param name="InputTokens">What was sent.</param>
/// <param name="OutputTokens">What came back.</param>
/// <param name="Pictures">How many images were made.</param>
public readonly record struct ModelUsage(int InputTokens, int OutputTokens, int Pictures);

/// <summary>
/// A recipe as a model described it: plain values, none of them believed yet.
/// </summary>
/// <remarks>
/// Deliberately loose — strings and nullable numbers, no value objects and no
/// invariants. It is the shape of an answer, not the shape of a recipe, and
/// every field still has to survive the domain before it becomes one. A model
/// that says an ingredient is "2 sprinkles of salt" produces a perfectly valid
/// instance of this and an ordinary failure one step later.
/// </remarks>
public sealed record DraftedRecipe
{
    /// <summary>What it is called.</summary>
    public string? Title { get; init; }

    /// <summary>A sentence or two about it.</summary>
    public string? Description { get; init; }

    /// <summary>How many it makes.</summary>
    public decimal? YieldAmount { get; init; }

    /// <summary>What it makes: servings, or a cake.</summary>
    public string? YieldLabel { get; init; }

    /// <summary>Minutes of hands-on work.</summary>
    public int? PrepMinutes { get; init; }

    /// <summary>Minutes of cooking.</summary>
    public int? CookMinutes { get; init; }

    /// <summary>The ingredient groups, in order.</summary>
    public IReadOnlyList<DraftedGroup> Groups { get; init; } = [];

    /// <summary>The steps, in order.</summary>
    public IReadOnlyList<DraftedStep> Steps { get; init; } = [];

    /// <summary>What to file it under.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>A heading and the lines under it, as a model described them.</summary>
public sealed record DraftedGroup
{
    /// <summary>The heading, or null for the implicit first group.</summary>
    public string? Name { get; init; }

    /// <summary>Its lines, in order.</summary>
    public IReadOnlyList<DraftedIngredient> Ingredients { get; init; } = [];
}

/// <summary>One ingredient line, as a model described it.</summary>
public sealed record DraftedIngredient
{
    /// <summary>How much.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>In what. Not yet known to be a unit this app has.</summary>
    public string? Unit { get; init; }

    /// <summary>The shoppable noun.</summary>
    public string? Name { get; init; }

    /// <summary>The preparation.</summary>
    public string? Note { get; init; }
}

/// <summary>One instruction, as a model described it.</summary>
/// <remarks>
/// Plain text, not segments. Which words in a step name an ingredient is a
/// question this app already answers, in <c>mentions</c> on the way in — asking
/// a model to mark them up as well would be two sources of truth for the same
/// fact, and the deterministic one is the one that can be tested.
/// </remarks>
public sealed record DraftedStep
{
    /// <summary>What this step is called, when it is called anything.</summary>
    public string? Title { get; init; }

    /// <summary>What to do.</summary>
    public string? Text { get; init; }

    /// <summary>How long it waits, when it waits.</summary>
    public int? DurationSeconds { get; init; }
}
