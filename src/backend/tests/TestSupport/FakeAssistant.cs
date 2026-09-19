using Application.Abstractions;
using Domain.Assistance;
using Domain.Shared;

namespace TestSupport;

/// <summary>
/// Answers as a model would, including badly.
/// </summary>
/// <remarks>
/// <para>
/// A queue rather than a single canned answer, because the interesting
/// behaviour is what a handler does with a <em>sequence</em> — a throttle then
/// a success, or an unusable answer then a good one.
/// </para>
/// <para>
/// It also keeps the last request, which is the only way to assert the one
/// thing that really matters about the adapters: that the instruction and the
/// untrusted material arrived in different fields and were never joined.
/// </para>
/// </remarks>
public sealed class FakeAssistant(AssistantKind? kind = null) : IAssistant
{
    private readonly Queue<Result<Composed>> compositions = new();
    private readonly Queue<Result<Drawn>> drawings = new();

    /// <inheritdoc />
    public AssistantKind Kind { get; } = kind ?? AssistantKind.Gemini;

    /// <summary>The last thing it was asked to compose.</summary>
    public Composition? LastComposition { get; private set; }

    /// <summary>
    /// The connection it was last called with.
    /// </summary>
    /// <remarks>
    /// The only way to assert the thing several providers at once is for: that
    /// each job reached the provider and model it was pointed at, rather than
    /// whichever one happened to be first.
    /// </remarks>
    public Connected? LastConnection { get; private set; }

    /// <summary>The last thing it was asked to draw.</summary>
    public Drawing? LastDrawing { get; private set; }

    /// <summary>How many times it was asked for anything.</summary>
    public int Calls { get; private set; }

    /// <summary>Queues the next answer to a compose.</summary>
    /// <param name="answer">What to say next.</param>
    public FakeAssistant WillCompose(Result<Composed> answer)
    {
        compositions.Enqueue(answer);

        return this;
    }

    /// <summary>Queues a plain success with the usage a caller expects.</summary>
    /// <param name="recipe">The draft to answer with.</param>
    /// <param name="usage">What to claim it consumed.</param>
    public FakeAssistant WillCompose(DraftedRecipe recipe, ModelUsage usage = default) =>
        WillCompose(Result<Composed>.Success(new Composed(recipe, usage)));

    /// <summary>Queues the next answer to a draw.</summary>
    /// <param name="answer">What to say next.</param>
    public FakeAssistant WillDraw(Result<Drawn> answer)
    {
        drawings.Enqueue(answer);

        return this;
    }

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
}

/// <summary>Hands out one fake, whatever provider is asked for.</summary>
/// <param name="assistant">The one to hand out.</param>
public sealed class FakeAssistants(FakeAssistant assistant) : IAssistants
{
    public Result<IAssistant> For(AssistantKind kind) => Result<IAssistant>.Success(assistant);

    /// <summary>A recognisable address, so a test can assert which was used.</summary>
    public string HomeOf(AssistantKind kind) => $"https://{kind?.Code}.example.com";

    /// <summary>A recognisable name, for the same reason.</summary>
    public string DefaultModelFor(AssistantKind kind, Capability capability) =>
        $"{kind?.Code}-default-{capability?.Code}";
}
