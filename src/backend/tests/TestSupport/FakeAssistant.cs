using System.Runtime.CompilerServices;
using Application.Abstractions;
using Domain.Assistance;
using Domain.Shared;

namespace TestSupport;

/// <summary>Answers as a model would, including badly.</summary>
/// <remarks>
/// A queue, to test sequences (a throttle then a success); it keeps the last request to assert
/// instruction and untrusted material were never joined.
/// </remarks>
public sealed class FakeAssistant(AssistantKind? kind = null) : IAssistant
{
    private readonly Queue<Result<Composed>> compositions = new();
    private readonly Queue<Result<Drawn>> drawings = new();

    public AssistantKind Kind { get; } = kind ?? AssistantKind.Gemini;

    public Composition? LastComposition { get; private set; }

    /// <summary>
    /// The connection it was last called with, to assert each job reached the provider and model it
    /// was pointed at.
    /// </summary>
    public Connected? LastConnection { get; private set; }

    public Drawing? LastDrawing { get; private set; }

    public int Calls { get; private set; }

    /// <summary>Queues the next answer to a compose.</summary>
    public FakeAssistant WillCompose(Result<Composed> answer)
    {
        compositions.Enqueue(answer);

        return this;
    }

    /// <summary>Queues a plain success with the usage a caller expects.</summary>
    public FakeAssistant WillCompose(DraftedRecipe recipe, ModelUsage usage = default) =>
        WillCompose(Result<Composed>.Success(new Composed(recipe, usage)));

    /// <summary>Queues the next answer to a draw.</summary>
    public FakeAssistant WillDraw(Result<Drawn> answer)
    {
        drawings.Enqueue(answer);

        return this;
    }

    /// <summary>What it says it offers, and whether it will answer at all.</summary>
    public Result<IReadOnlyList<ModelInfo>> Models { get; set; } =
        Result<IReadOnlyList<ModelInfo>>.Success(
        [
            new ModelInfo("fast-one", "Fast one", CanDraw: false),
            new ModelInfo("draws-one", "Draws one", CanDraw: true)
        ]);

    public Task<Result<Composed>> ComposeAsync(
        Connected @using,
        Composition request,
        CancellationToken cancellationToken)
    {
        LastComposition = request;
        LastConnection = @using;
        Calls++;

        return Task.FromResult(compositions.Count > 0
            ? compositions.Dequeue()
            : Result<Composed>.Failure(AssistanceErrors.Unavailable));
    }

    /// <summary>
    /// The same queued answer, in pieces: a success arrives as a thin part then the whole, as a
    /// real stream does.
    /// </summary>
    public async IAsyncEnumerable<Composing> ComposeStreamAsync(
        Connected @using,
        Composition request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var answered = await ComposeAsync(@using, request, cancellationToken).ConfigureAwait(false);

        foreach (var part in answered.Match(Written, Stopped))
        {
            yield return part;
        }
    }

    private static IEnumerable<Composing> Written(Composed answer) =>
    [
        new Composing { Recipe = new DraftedRecipe { Title = answer.Recipe.Title } },
        new Composing { Recipe = answer.Recipe, Finished = true, Usage = answer.Usage }
    ];

    private static IEnumerable<Composing> Stopped(Error failure) =>
    [
        new Composing { Recipe = new DraftedRecipe(), Finished = true, Failure = failure }
    ];

    public Task<Result<Drawn>> DrawAsync(
        Connected @using,
        Drawing request,
        CancellationToken cancellationToken)
    {
        LastDrawing = request;
        LastConnection = @using;
        Calls++;

        return Task.FromResult(drawings.Count > 0
            ? drawings.Dequeue()
            : Result<Drawn>.Failure(AssistanceErrors.Unavailable));
    }

    public Task<Result<IReadOnlyList<ModelInfo>>> ListModelsAsync(
        Connected @using,
        CancellationToken cancellationToken)
    {
        LastConnection = @using;

        return Task.FromResult(Models);
    }
}

/// <summary>Hands out one fake, whatever provider is asked for.</summary>
public sealed class FakeAssistants(FakeAssistant assistant) : IAssistants
{
    public Result<IAssistant> For(AssistantKind kind) => Result<IAssistant>.Success(assistant);

    public string HomeOf(AssistantKind kind) => $"https://{kind?.Code}.example.com";

    public string DefaultModelFor(AssistantKind kind, Capability capability) =>
        $"{kind?.Code}-default-{capability?.Code}";
}
