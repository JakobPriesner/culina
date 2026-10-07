using System.Runtime.CompilerServices;
using Application.Abstractions;
using Domain.Assistance;
using Domain.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Assistance;

/// <summary>Reads a recipe off an <see cref="IChatClient"/> as it is written, shared by the two adapters that offer one.</summary>
internal static class ChatStream
{
    /// <summary>
    /// Asks for a recipe and yields it as it arrives. <c>recognised</c> maps a provider-owned failure to an error, or null to let it propagate.
    /// </summary>
    /// <remarks>
    /// Every ending (finished, provider failure, unusable answer) yields a <see cref="Composing.Finished"/> part: it settles the ledger,
    /// and without it the reservation would hold until the month turned.
    /// </remarks>
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

        // Advanced by hand: a `yield` cannot sit inside a `try` that catches, and provider calls need one.
        var parts = client
            .GetStreamingResponseAsync(ChatAsk.Conversation(request), ChatAsk.Options(), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                ChatResponseUpdate? part = null;
                Exception? thrown = null;

                // The failure is caught here and acted on below, for the same reason.
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
                    var failure = recognised(thrown) ?? AssistanceErrors.Unavailable;

                    AssistanceLogs.CallFailed(logger, kind.Code, failure.Code, thrown);

                    yield return Stopped(answer, failure);

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

    /// <summary>The last part of a stream that ended badly. Keeps the recipe so far (it was paid for); no usage.</summary>
    private static Composing Stopped(PartialRecipe answer, Error failure) => new()
    {
        Recipe = answer.SoFar(),
        Finished = true,
        Failure = failure
    };

    /// <summary>The counts, which arrive once and not necessarily at the end; kept so mid-stream usage is not overwritten with zero.</summary>
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

/// <summary>How a recipe is asked for, in one place for all three callers. The instruction (system) and the material (user) never meet.</summary>
internal static class ChatAsk
{
    /// <summary>The two turns, kept apart.</summary>
    internal static List<ChatMessage> Conversation(Composition request) =>
    [
        new(ChatRole.System, request.Instruction),
        new(ChatRole.User, Material(request))
    ];

    /// <summary>A structured output, asked for strictly so the provider enforces the schema rather than merely seeing it.</summary>
    /// <remarks>
    /// Set through <c>AdditionalProperties</c> (the only route the abstraction offers); providers that do not know the key ignore it.
    /// </remarks>
    internal static ChatOptions Options() => new()
    {
        ResponseFormat = ChatResponseFormat.ForJsonSchema(RecipeSchema.AsJson, RecipeSchema.Name),
        AdditionalProperties = new AdditionalPropertiesDictionary { ["strict"] = true },
        MaxOutputTokens = Composition.MostOutputTokens
    };

    /// <summary>What somebody pasted, typed or photographed. Untrusted.</summary>
    private static List<AIContent> Material(Composition request)
    {
        List<AIContent> parts = [];

        if (request.Material is { Length: > 0 } material)
        {
            parts.Add(new TextContent(material));
        }

        foreach (var picture in request.Pictures)
        {
            parts.Add(new DataContent(picture.Content, picture.MediaType));
        }

        return parts;
    }
}
