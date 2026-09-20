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

/// <summary>
/// Talks to OpenAI's models, and to anything that answers in their shape.
/// </summary>
/// <remarks>
/// <para>
/// OpenAI's own client library rather than this app's reading of their
/// documentation. What that buys is the fault with no symptom: a field written
/// <c>b64_json</c> where a hand-written record expected <c>b64Json</c> bound to
/// null, and a picture that had arrived was reported as an answer that could
/// not be read. Those types are now maintained by the people who change the
/// wire format.
/// </para>
/// <para>
/// Composition goes through <see cref="IChatClient"/>, so the two providers
/// that offer one are asked for text in identical words and differ only where
/// they really differ. Drawing and listing have no such abstraction and use the
/// SDK directly.
/// </para>
/// <para>
/// The system role survives, which matters more to this feature than anything
/// else about it: the instruction is a system message and the untrusted
/// material is a user message — two kinds of thing rather than two halves of
/// one.
/// </para>
/// <para>
/// The second half of "anything that answers in their shape" is why the address
/// is overridable. A great many self-hosted runners and gateways speak this API
/// and nothing else, so this adapter is also the adapter for those.
/// </para>
/// <para>
/// Stateless with respect to configuration: the key, the address and the model
/// all arrive with the call, so one instance serves however many connections an
/// administrator has set up.
/// </para>
/// </remarks>
/// <param name="http">The shared client, whose rules the SDK is made to keep.</param>
/// <param name="logger">Records what a provider refused, and why.</param>
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

        List<ChatMessage> conversation =
        [
            new(ChatRole.System, request.Instruction),
            new(ChatRole.User, Material(request))
        ];

        var options = new ChatOptions
        {
            ResponseFormat = ChatResponseFormat.ForJsonSchema(RecipeSchema.AsJson, RecipeSchema.Name)
        };

        try
        {
            var answered = await ChatWith(@using)
                .GetResponseAsync(conversation, options, cancellationToken)
                .ConfigureAwait(false);

            return Read(answered);
        }
        catch (Exception failure) when (Expected(failure))
        {
            return Failure(failure);
        }
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
            var drawn = await Client(@using)
                .GetImageClient(@using.Model)
                .GenerateImageAsync(
                    request.Subject,
                    // One square picture. Not a choice this app offers, because
                    // every other combination costs more and none of them makes
                    // a recipe card look different.
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
            return Failure(failure);
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
                    .Select(model => model.Id)
                    .Where(id => id.Length > 0)
                    .Select(id => new ModelInfo(id, id, DrawingModel.Draws(id)))
                    .OrderBy(model => model.Id, StringComparer.Ordinal)
            ];

            return Result<IReadOnlyList<ModelInfo>>.Success(
                ModelCatalogue.Narrow(everything, model => Usable(model.Id)));
        }
        catch (Exception failure) when (Expected(failure))
        {
            return Failure(failure);
        }
    }

    /// <summary>
    /// The material, as the parts of the user message.
    /// </summary>
    /// <remarks>
    /// The instruction is the system message and is never here. This builds only
    /// the untrusted half — what somebody pasted, typed or photographed — and
    /// the two are never concatenated.
    /// </remarks>
    private static List<AIContent> Material(Composition request)
    {
        List<AIContent> parts = [];

        if (request.Material is { Length: > 0 } material)
        {
            parts.Add(new TextContent(material));
        }

        if (!request.Picture.IsEmpty)
        {
            parts.Add(new DataContent(
                request.Picture,
                request.PictureMediaType ?? "image/jpeg"));
        }

        return parts;
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
            // The model answered with something that is not the shape it was
            // given. Ordinary rather than exceptional, and the person asking
            // gets to try again.
            return AssistanceErrors.UnusableAnswer;
        }
    }

    private static ModelUsage Usage(ChatResponse answered) => new(
        (int)(answered.Usage?.InputTokenCount ?? 0),
        (int)(answered.Usage?.OutputTokenCount ?? 0),
        Pictures: 0);

    /// <summary>
    /// The client for one connection.
    /// </summary>
    /// <remarks>
    /// Built per call rather than held: the key and the address belong to the
    /// connection being used, and an instance of this class serves all of them.
    /// The transport is the app's own, so the SDK inherits the rules the
    /// hand-written client had — no redirects with a key attached, one pool,
    /// one deadline.
    /// </remarks>
    private OpenAIClient Client(Connected @using) => new(
        new ApiKeyCredential(@using.ApiKey),
        new OpenAIClientOptions
        {
            Endpoint = Endpoint(@using.BaseUrl),
            Transport = new HttpClientPipelineTransport(http.Client)
        });

    /// <summary>
    /// Where the client should be pointed.
    /// </summary>
    /// <param name="baseUrl">The address the connection carries.</param>
    /// <remarks>
    /// <para>
    /// The version segment belongs to the endpoint this library is given: it
    /// appends <c>models</c> or <c>responses</c> to whatever it is handed, so
    /// an address without <c>/v1</c> asks for <c>api.openai.com/models</c> and
    /// is answered with a 404 — which reads on the settings screen as a
    /// provider that has nothing to offer.
    /// </para>
    /// <para>
    /// Added only when it is missing, because an administrator pointing this at
    /// a gateway may reasonably paste either form, and the documentation for
    /// most of them prints the one ending in <c>/v1</c>.
    /// </para>
    /// </remarks>
    private static Uri Endpoint(string baseUrl)
    {
        var address = baseUrl.TrimEnd('/');

        return new Uri(address.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? address
            : address + "/v1");
    }

    private IChatClient ChatWith(Connected @using) =>
        Client(@using).GetChatClient(@using.Model).AsIChatClient();

    /// <summary>
    /// Whether this one could write a recipe.
    /// </summary>
    /// <remarks>
    /// This listing is everything the account can reach — embeddings, speech,
    /// transcription, moderation, the lot — and there is no field saying which
    /// is which. Filtering by name is crude and is the only thing available;
    /// erring towards keeping a model means an odd entry in a list, where
    /// erring the other way means a model somebody wanted is missing. And a
    /// key that can reach only speech models would be filtered to nothing,
    /// which <see cref="ModelCatalogue.Narrow"/> refuses to do.
    /// </remarks>
    private static bool Usable(string id) =>
        id.Length > 0
        && !id.Contains("embedding", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("moderation", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("whisper", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("tts", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("audio", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("realtime", StringComparison.OrdinalIgnoreCase);

    /// <summary>The failures that are the provider's rather than this app's.</summary>
    private static bool Expected(Exception failure) =>
        failure is ClientResultException or HttpRequestException or OperationCanceledException
            or IOException or InvalidOperationException;

    /// <summary>
    /// What a refusal means.
    /// </summary>
    /// <remarks>
    /// A refused credential is carried as itself this far. It is turned back
    /// into "unavailable" before it can reach somebody who is cooking, but the
    /// settings screen and the ledger are read by the person holding the key,
    /// and telling them their key was refused is the whole of what they need.
    /// </remarks>
    private static Error Failure(Exception failure) => failure switch
    {
        ClientResultException { Status: 429 } => AssistanceErrors.Throttled,
        // Forbidden as well as unauthorized: a key scoped to inference and not
        // to reading the catalogue answers 403 while signing every other call
        // in this app perfectly well.
        ClientResultException { Status: 401 or 403 } => AssistanceErrors.Rejected,
        // A content filter and a malformed request both answer 400. The
        // caller's options are the same either way, and the log line tells
        // them apart.
        ClientResultException { Status: 400 } => AssistanceErrors.Refused,
        _ => AssistanceErrors.Unavailable
    };
}
