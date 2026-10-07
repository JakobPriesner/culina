using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Application.Abstractions;
using Application.Assistance;
using Domain.Assistance;
using Domain.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Images;
using OpenAiImageOptions = OpenAI.Images.ImageGenerationOptions;

namespace Infrastructure.Assistance;

/// <summary>Talks to OpenAI's models, and to anything that answers in their shape.</summary>
/// <remarks>
/// Uses OpenAI's own client library (a hand-written wire record once bound <c>b64_json</c> to null).
/// Composition goes through <see cref="IChatClient"/>; drawing and listing use the SDK directly. The
/// system instruction stays a system message and untrusted material a user message. The address is
/// overridable because many self-hosted runners and gateways speak this API. Stateless: key, address
/// and model arrive with each call.
/// </remarks>
internal sealed class OpenAiAssistant(
    AssistantHttp http,
    ILogger<OpenAiAssistant> logger) : IAssistant
{
    public AssistantKind Kind => AssistantKind.OpenAi;

    public async Task<Result<Composed>> ComposeAsync(
        Connected @using,
        Composition request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var answered = await ChatWith(@using)
                .GetResponseAsync(ChatAsk.Conversation(request), ChatAsk.Options(), cancellationToken)
                .ConfigureAwait(false);

            return Read(answered);
        }
        catch (Exception failure) when (Expected(failure))
        {
            return Failed(failure);
        }
    }

    public IAsyncEnumerable<Composing> ComposeStreamAsync(
        Connected @using,
        Composition request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);
        ArgumentNullException.ThrowIfNull(request);

        return ChatStream.ComposeAsync(
            ChatWith(@using),
            request,
            Kind,
            logger,
            Recognised,
            cancellationToken);
    }

    public async Task<Result<Drawn>> DrawAsync(
        Connected @using,
        Drawing request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var drawn = await Client(@using, drawing: true)
                .GetImageClient(@using.Model)
                .GenerateImageAsync(
                    request.Subject,
                    // One square picture: every other size costs more and changes nothing on a recipe card.
                    new OpenAiImageOptions { Size = GeneratedImageSize.W1024xH1024 },
                    cancellationToken)
                .ConfigureAwait(false);

            if (drawn.Value?.ImageBytes is not { } bytes)
            {
                AssistanceLogs.EmptyAnswer(logger, Kind.Code, finishReason: null);

                return AssistanceErrors.UnusableAnswer;
            }

            return new Drawn(new MemoryStream(bytes.ToArray()), new ModelUsage(0, 0, Pictures: 1));
        }
        catch (Exception failure) when (Expected(failure))
        {
            return Failed(failure);
        }
    }

    public async Task<Result<IReadOnlyList<ModelInfo>>> ListModelsAsync(
        Connected @using,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);

        try
        {
            var listed = await Client(@using)
                .GetOpenAIModelClient()
                .GetModelsAsync(cancellationToken)
                .ConfigureAwait(false);

            IReadOnlyList<ModelInfo> everything =
            [
                .. listed.Value
                    .Where(model => model.Id.Length > 0)
                    .Select(model => new ModelInfo(
                        model.Id,
                        model.Id,
                        DrawingModel.Draws(model.Id),
                        model.CreatedAt))
            ];

            return Result<IReadOnlyList<ModelInfo>>.Success(
                ModelCatalogue.Newest(ModelCatalogue.Narrow(everything, model => Usable(model.Id))));
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
            // The model answered in the wrong shape: ordinary, and the person can try again.
            return AssistanceErrors.UnusableAnswer;
        }
    }

    private static ModelUsage Usage(ChatResponse answered) => new(
        (int)(answered.Usage?.InputTokenCount ?? 0),
        (int)(answered.Usage?.OutputTokenCount ?? 0),
        Pictures: 0);

    // Built per call: key and address belong to the connection. The transport is the app's own, so the
    // SDK keeps the no-redirects, one-pool, one-deadline rules.
    private OpenAIClient Client(Connected @using, bool drawing = false) => new(
        new ApiKeyCredential(@using.ApiKey),
        new OpenAIClientOptions
        {
            Endpoint = Endpoint(@using.BaseUrl),
            Transport = new HttpClientPipelineTransport(http.Client(drawing)),
            // The library keeps a deadline of its own; the shorter of the two wins.
            NetworkTimeout = http.Client(drawing).Timeout
        });

    // The library appends models/responses to the endpoint, so a missing /v1 would 404. Added only when
    // missing since admins may paste either form.
    private static Uri Endpoint(string baseUrl)
    {
        var address = baseUrl.TrimEnd('/');

        return new Uri(address.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? address
            : address + "/v1");
    }

    private IChatClient ChatWith(Connected @using) =>
        Client(@using).GetChatClient(@using.Model).AsIChatClient();

    // The listing includes embeddings, speech, moderation and more with no field to tell them apart, so
    // filter by name, erring towards keeping a model. Narrow refuses to filter down to nothing.
    private static bool Usable(string id) =>
        id.Length > 0
        && !id.Contains("embedding", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("moderation", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("whisper", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("tts", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("audio", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("realtime", StringComparison.OrdinalIgnoreCase);

    // One value instead of a catch filter plus body, because a stream cannot catch around a yield.
    private static Error? Recognised(Exception failure) =>
        Expected(failure) ? Failure(failure) : null;

    private Error Failed(Exception failure)
    {
        var error = Failure(failure);

        AssistanceLogs.CallFailed(logger, Kind.Code, error.Code, failure);

        return error;
    }

    private static bool Expected(Exception failure) =>
        failure is ClientResultException or HttpRequestException or OperationCanceledException
            or IOException or InvalidOperationException;

    // A refused credential is carried as itself: it becomes "unavailable" before reaching a cook, but
    // the settings screen and ledger are read by the key holder.
    private static Error Failure(Exception failure) => failure switch
    {
        ClientResultException { Status: 429 } => AssistanceErrors.Throttled,
        // 403 too: a key scoped to inference answers 403 on the catalogue.
        ClientResultException { Status: 401 or 403 } => AssistanceErrors.Rejected,
        // A content filter and a malformed request both answer 400; the log tells them apart.
        ClientResultException { Status: 400 } => AssistanceErrors.Refused,
        ClientResultException { Status: 404 } => AssistanceErrors.ModelMissing,
        _ => AssistanceErrors.Unavailable
    };
}
