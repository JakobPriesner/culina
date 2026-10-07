using Domain.Assistance;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>One model provider, as the rest of the app needs it.</summary>
/// <remarks>
/// Instructions are this application's business, so compose, improve and read-from-photo share one method; drawing is a second.
/// Each call is told its connection and model rather than reading settings, so one instance can use different models per job.
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

    /// <summary>Asks for a recipe and hands it over as it is written.</summary>
    /// <param name="using">Where to reach the provider, and which model.</param>
    /// <param name="request">What to do, and what to do it to.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <remarks>
    /// Each part carries the recipe as far as the half-written JSON can be read. Provider failures never throw:
    /// the last part has <see cref="Composing.Finished"/> and carries the cost or the reason, so the ledger settles the same either way.
    /// </remarks>
    IAsyncEnumerable<Composing> ComposeStreamAsync(
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

    /// <summary>What this provider currently offers, so a model is chosen from a list instead of typed.</summary>
    /// <param name="using">Where to reach it. The model on it is ignored.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    Task<Result<IReadOnlyList<ModelInfo>>> ListModelsAsync(
        Connected @using,
        CancellationToken cancellationToken);
}

/// <summary>One model a provider offers.</summary>
/// <param name="Id">What to send as the model name.</param>
/// <param name="Label">What to show, where the provider says something nicer.</param>
/// <param name="CanDraw">Whether it makes pictures.</param>
/// <param name="Added">When the provider published it, where the provider says.</param>
/// <remarks>
/// <paramref name="CanDraw"/> is the adapter's best guess; no provider states it. <paramref name="Added"/> is null for providers
/// (Google) that do not date their catalogue, rather than inventing a date.
/// </remarks>
public sealed record ModelInfo(
    string Id,
    string Label,
    bool CanDraw,
    DateTimeOffset? Added = null);

/// <summary>One provider, ready to be called: key decrypted, address and model already resolved.</summary>
/// <param name="ApiKey">The credential, in the clear. Empty where none is needed.</param>
/// <param name="BaseUrl">Where to send the request.</param>
/// <param name="Model">Which model to ask.</param>
public sealed record Connected(string ApiKey, string BaseUrl, string Model);

/// <summary>A request for a recipe, with the instruction kept apart from the material.</summary>
/// <remarks>
/// The separation is the prompt-injection defence: <see cref="Instruction"/> is ours, <see cref="Material"/> and
/// <see cref="Pictures"/> are whatever somebody pasted. Adapters never concatenate them and only read the reply as data.
/// </remarks>
public sealed record Composition
{
    /// <summary>The most a model may write in answer, thinking included.</summary>
    /// <remarks>
    /// Sent with every ask because the pre-call reservation is a fixed sum; <c>AssistantRun</c>'s estimate is sized to this.
    /// </remarks>
    public const int MostOutputTokens = 8_192;

    /// <summary>Which capability this is, for the ledger.</summary>
    public required Capability Capability { get; init; }

    /// <summary>The language the answer must be written in.</summary>
    public required Language Language { get; init; }

    /// <summary>What to do. Written here, never by a caller of the API.</summary>
    public required string Instruction { get; init; }

    /// <summary>What to do it to. Untrusted.</summary>
    public string? Material { get; init; }

    /// <summary>Photographs or screenshots to read a recipe out of, in reading order. Untrusted, metadata already stripped.</summary>
    public IReadOnlyList<RecipePicture> Pictures { get; init; } = [];
}

/// <summary>What the model said, and what it cost.</summary>
/// <param name="Recipe">The answer, still unvalidated.</param>
/// <param name="Usage">What to write in the ledger.</param>
public sealed record Composed(DraftedRecipe Recipe, ModelUsage Usage);

/// <summary>A recipe part-written, or the moment one stopped being written.</summary>
public sealed record Composing
{
    /// <summary>The recipe as far as it has been written. Never null, often thin.</summary>
    public required DraftedRecipe Recipe { get; init; }

    /// <summary>Whether this is the last part. Exactly one part has it.</summary>
    public bool Finished { get; init; }

    /// <summary>What the call consumed, on the last part and nothing before it.</summary>
    public ModelUsage Usage { get; init; }

    /// <summary>Why it stopped, when it stopped badly. Returned on the item rather than thrown.</summary>
    public Error? Failure { get; init; }
}

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

/// <summary>What one call consumed, as the provider reported it, never estimated here.</summary>
/// <param name="InputTokens">What was sent.</param>
/// <param name="OutputTokens">What came back.</param>
/// <param name="Pictures">How many images were made.</param>
public readonly record struct ModelUsage(int InputTokens, int OutputTokens, int Pictures);

/// <summary>A recipe as a model described it: plain values, none believed until the domain accepts them.</summary>
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
/// <remarks>Plain text: which words name an ingredient is already answered deterministically by <c>mentions</c>.</remarks>
public sealed record DraftedStep
{
    /// <summary>What this step is called, when it is called anything.</summary>
    public string? Title { get; init; }

    /// <summary>What to do.</summary>
    public string? Text { get; init; }

    /// <summary>How long it waits, when it waits.</summary>
    public int? DurationSeconds { get; init; }
}

/// <summary>One page or screenshot of the source recipe.</summary>
public sealed record RecipePicture(ReadOnlyMemory<byte> Content, string MediaType);
