using System.Runtime.CompilerServices;
using System.Text.Json;
using Application.Abstractions;
using Application.Assistance;
using Domain.Assistance;
using Domain.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace Infrastructure.Assistance;

/// <summary>
/// Talks to a model running on your own hardware: free, private, and works offline.
/// </summary>
/// <remarks>
/// OllamaSharp rather than the OpenAI-compatible endpoint: the native API enforces a JSON schema
/// directly and reports usage under its own names. Needs no key; the address is required.
/// </remarks>
internal sealed class OllamaAssistant(
    AssistantHttp http,
    ILogger<OllamaAssistant> logger) : IAssistant
{
    /// <summary>
    /// Where Ollama listens by default; only a hint, since a wrong default is worse than an empty
    /// box.
    /// </summary>
    internal const string UsualAddress = "http://localhost:11434";

    public AssistantKind Kind => AssistantKind.Ollama;

    public async Task<Result<Composed>> ComposeAsync(
        Connected @using,
        Composition request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var transport = http.ClientFor(@using.BaseUrl);
            using var client = new OllamaApiClient(transport, @using.Model);

            // Through the interface: the client's own overload is not the shape the OpenAI adapter
            // speaks.
            var answered = await ((IChatClient)client)
                .GetResponseAsync(ChatAsk.Conversation(request), ChatAsk.Options(), cancellationToken)
                .ConfigureAwait(false);

            return Read(answered);
        }
        catch (Exception failure) when (Expected(failure))
        {
            return Failed(failure);
        }
    }

    public async IAsyncEnumerable<Composing> ComposeStreamAsync(
        Connected @using,
        Composition request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);
        ArgumentNullException.ThrowIfNull(request);

        // Disposed when the enumeration ends, including a caller who walks away halfway.
        using var transport = http.ClientFor(@using.BaseUrl);
        using var client = new OllamaApiClient(transport, @using.Model);

        var parts = ChatStream.ComposeAsync(
            (IChatClient)client,
            request,
            Kind,
            logger,
            Recognised,
            cancellationToken);

        await foreach (var part in parts.ConfigureAwait(false))
        {
            yield return part;
        }
    }

    /// <summary>
    /// Refused, because nothing this runner serves draws; the settings screen does not offer it.
    /// </summary>
    public Task<Result<Drawn>> DrawAsync(
        Connected @using,
        Drawing request,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result<Drawn>.Failure(AssistanceErrors.DrawingNotSupported));

    public async Task<Result<IReadOnlyList<ModelInfo>>> ListModelsAsync(
        Connected @using,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);

        try
        {
            using var transport = http.ClientFor(@using.BaseUrl);
            using var client = new OllamaApiClient(transport, @using.Model);

            var pulled = await client.ListLocalModelsAsync(cancellationToken).ConfigureAwait(false);

            IReadOnlyList<ModelInfo> offered =
            [
                .. pulled
                    .Where(model => (model.Name ?? string.Empty).Length > 0)
                    // Whether a model draws is read from its name as elsewhere; expected to say no
                    // to all. The date is when it was pulled or last changed.
                    .Select(model => new ModelInfo(
                        model.Name!,
                        model.Name!,
                        DrawingModel.Draws(model.Name!),
                        model.ModifiedAt))
            ];

            return Result<IReadOnlyList<ModelInfo>>.Success(ModelCatalogue.Newest(offered));
        }
        catch (Exception failure) when (Expected(failure))
        {
            return Failed(failure);
        }
    }

    private Result<Composed> Read(ChatResponse answered)
    {
        if (answered.Text is not { Length: > 0 } json)
        {
            AssistanceLogs.EmptyAnswer(logger, Kind.Code, answered.FinishReason?.Value);

            return AssistanceErrors.UnusableAnswer;
        }

        try
        {
            var answer = JsonSerializer.Deserialize<RecipeAnswer>(json, AssistantHttp.Json);

            return answer is null
                ? AssistanceErrors.UnusableAnswer
                : new Composed(answer.ToDraft(), Usage(answered));
        }
        catch (JsonException)
        {
            // A schema-bound local model still occasionally answers with something else; the person
            // can retry.
            return AssistanceErrors.UnusableAnswer;
        }
    }

    private static ModelUsage Usage(ChatResponse answered) => new(
        (int)(answered.Usage?.InputTokenCount ?? 0),
        (int)(answered.Usage?.OutputTokenCount ?? 0),
        Pictures: 0);

    /// <summary>
    /// What a thrown failure means, or null where it is not the runner's; streams need a verdict,
    /// not a catch.
    /// </summary>
    private static Error? Recognised(Exception failure) =>
        Expected(failure) ? Failure(failure) : null;

    private Error Failed(Exception failure)
    {
        var error = Failure(failure);

        AssistanceLogs.CallFailed(logger, Kind.Code, error.Code, failure);

        return error;
    }

    private static bool Expected(Exception failure) =>
        failure is HttpRequestException or OperationCanceledException or IOException
            or InvalidOperationException or JsonException;

    /// <summary>
    /// What a refusal means: a runner that is not there, is busy, or lacks the model.
    /// </summary>
    private static Error Failure(Exception failure) =>
        failure is HttpRequestException { StatusCode: { } status }
            ? status switch
            {
                System.Net.HttpStatusCode.TooManyRequests => AssistanceErrors.Throttled,
                System.Net.HttpStatusCode.NotFound => AssistanceErrors.ModelMissing,
                System.Net.HttpStatusCode.BadRequest => AssistanceErrors.Refused,
                _ => AssistanceErrors.Unavailable
            }
            : AssistanceErrors.Unavailable;
}
