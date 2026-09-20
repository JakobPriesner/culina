using System.Runtime.CompilerServices;
using Application.Abstractions;
using Domain.Assistance;
using Domain.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Assistance;

/// <summary>
/// Reading a recipe off an <see cref="IChatClient"/> as it is written.
/// </summary>
/// <remarks>
/// <para>
/// Shared by the two adapters whose providers offer that interface, for the
/// same reason their <c>ComposeAsync</c> methods read alike: what differs
/// between OpenAI and a model on your own machine is where the client points
/// and what its refusals mean, and neither of those is the loop.
/// </para>
/// <para>
/// The loop is the part that is easy to get wrong twice. A streaming call ends
/// in three ways — it finishes, it throws something the provider owns, or it
/// finishes having said nothing usable — and every one of them has to end with
/// a part carrying <see cref="Composing.Finished"/>, because that part is what
/// settles the ledger. A provider whose stream simply stopped would otherwise
/// hold its reservation against the budget until the month turned.
/// </para>
/// </remarks>
internal static class ChatStream
{
    /// <summary>Asks for a recipe and yields it as it arrives.</summary>
    /// <param name="client">The provider, already pointed and credentialled.</param>
    /// <param name="request">What to do, and what to do it to.</param>
    /// <param name="kind">Which provider, for the log line.</param>
    /// <param name="logger">Records an answer that could not be read.</param>
    /// <param name="recognised">
    /// What a thrown failure means to this provider, or null where the failure
    /// is not one the provider owns and should not be swallowed.
    /// </param>
    /// <param name="cancellationToken">Cancels the call.</param>
    internal static async IAsyncEnumerable<Composing> ComposeAsync(
        IChatClient client,
        Composition request,
        AssistantKind kind,
        ILogger logger,
        Func<Exception, Error?> recognised,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var answer = new PartialRecipe();
        var usage = default(ModelUsage);
        string? finishReason = null;

        // Advanced by hand rather than with `await foreach`, because a `yield`
        // may not live inside a `try` that catches — and every part of this
        // that talks to the provider has to be inside one.
        var parts = client
            .GetStreamingResponseAsync(ChatAsk.Conversation(request), ChatAsk.Options(), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                ChatResponseUpdate? part = null;
                Exception? thrown = null;

                // A `yield` may live in a `try` with a `finally` and not in one
                // with a `catch`, so the failure is caught here and acted on
                // below rather than where it happened.
                try
                {
                    if (await parts.MoveNextAsync().ConfigureAwait(false))
                    {
                        part = parts.Current;
                    }
                }
                catch (Exception failure) when (recognised(failure) is not null)
                {
                    thrown = failure;
                }

                if (thrown is not null)
                {
                    yield return Stopped(answer, recognised(thrown) ?? AssistanceErrors.Unavailable);

                    yield break;
                }

                if (part is null)
                {
                    break;
                }

                answer.Add(part.Text);
                usage = Counted(part, usage);
                finishReason ??= part.FinishReason?.Value;

                if (answer.Read() is { } soFar)
                {
                    yield return new Composing { Recipe = soFar };
                }
            }
        }
        finally
        {
            await parts.DisposeAsync().ConfigureAwait(false);
        }

        if (answer.ReadWhole() is not { } written)
        {
            AssistanceLogs.EmptyAnswer(logger, kind.Code, finishReason);

            yield return Stopped(answer, AssistanceErrors.UnusableAnswer);

            yield break;
        }

        yield return new Composing { Recipe = written, Finished = true, Usage = usage };
    }

    /// <summary>
    /// The last part of a stream that ended badly.
    /// </summary>
    /// <remarks>
    /// It still carries the recipe, and deliberately: a provider that cut out
    /// after the ingredients wrote something worth keeping, and the screen can
    /// offer it beside the reason rather than throwing away work that was paid
    /// for. No usage, because a call that did not finish did not report any.
    /// </remarks>
    private static Composing Stopped(PartialRecipe answer, Error failure) => new()
    {
        Recipe = answer.SoFar(),
        Finished = true,
        Failure = failure
    };

    /// <summary>
    /// The counts, which arrive once and not necessarily at the end.
    /// </summary>
    /// <remarks>
    /// Kept rather than overwritten: a provider that sends usage mid-stream and
    /// nothing after it would otherwise be recorded as having cost nothing,
    /// which is the exact fault the SDKs were adopted to remove.
    /// </remarks>
    private static ModelUsage Counted(ChatResponseUpdate part, ModelUsage soFar)
    {
        var counted = part.Contents.OfType<UsageContent>().FirstOrDefault()?.Details;

        return counted is null
            ? soFar
            : new ModelUsage(
                (int)(counted.InputTokenCount ?? soFar.InputTokens),
                (int)(counted.OutputTokenCount ?? soFar.OutputTokens),
                Pictures: 0);
    }
}

/// <summary>
/// How a recipe is asked for, wherever it is asked for.
/// </summary>
/// <remarks>
/// <para>
/// One place, because there are three callers — the two providers that answer
/// at once and the loop that reads one being written — and three copies of
/// "what we ask a model for" is three things to keep in step. The first time
/// they drifted, two of them asked for a shape and the third asked for nothing
/// in particular.
/// </para>
/// <para>
/// The instruction and the material never meet. The instruction is the system
/// message and is written by this application; the material is a user message
/// and is whatever somebody pasted, typed or photographed — which on a shared
/// instance means whatever somebody <em>else</em> pasted, typed or
/// photographed.
/// </para>
/// </remarks>
internal static class ChatAsk
{
    /// <summary>The two turns, kept apart.</summary>
    /// <param name="request">What to do, and what to do it to.</param>
    internal static List<ChatMessage> Conversation(Composition request) =>
    [
        new(ChatRole.System, request.Instruction),
        new(ChatRole.User, Material(request))
    ];

    /// <summary>
    /// A structured output, asked for strictly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>strict</c> is the difference between a schema the provider enforces
    /// while it decodes and one it was merely shown. Without it the client
    /// library still rewrites the schema to require every property — so the
    /// answer was being asked for in a shape nothing held it to, and the one
    /// failure mode that survives structured output, an answer that is not the
    /// shape it was given, stayed possible for no benefit.
    /// </para>
    /// <para>
    /// Set through <c>AdditionalProperties</c> because that is the only way the
    /// abstraction offers: <c>ChatResponseFormat.ForJsonSchema</c> has no
    /// parameter for it, and the OpenAI client reads this key. A provider that
    /// does not know the key ignores it, which is the right behaviour for one
    /// whose schemas are always enforced — a model on your own machine is
    /// decoded against the grammar either way.
    /// </para>
    /// </remarks>
    internal static ChatOptions Options() => new()
    {
        ResponseFormat = ChatResponseFormat.ForJsonSchema(RecipeSchema.AsJson, RecipeSchema.Name),
        AdditionalProperties = new AdditionalPropertiesDictionary { ["strict"] = true }
    };

    /// <summary>What somebody pasted, typed or photographed. Untrusted.</summary>
    private static List<AIContent> Material(Composition request)
    {
        List<AIContent> parts = [];

        if (request.Material is { Length: > 0 } material)
        {
            parts.Add(new TextContent(material));
        }

        if (!request.Picture.IsEmpty)
        {
            parts.Add(new DataContent(request.Picture, request.PictureMediaType ?? "image/jpeg"));
        }

        return parts;
    }
}
