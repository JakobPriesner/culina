using System.Text.Json;
using Application.Abstractions;
using Domain.Assistance;
using Domain.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace Infrastructure.Assistance;

/// <summary>
/// Talks to a model running on your own hardware.
/// </summary>
/// <remarks>
/// <para>
/// The provider this app is most obviously for. Culina is self-hosted, so the
/// household that installed it is the household most likely to already have a
/// model running in the same house — and for them the assistant costs nothing,
/// sends nothing to anybody, and works with the network unplugged. Every
/// privacy sentence elsewhere in this feature is about the other two providers;
/// this one has none to write.
/// </para>
/// <para>
/// OllamaSharp rather than the OpenAI-compatible endpoint, and the reasons are
/// the same ones that made this class worth its own file when it spoke HTTP:
/// the native API takes a JSON schema directly and the runner enforces it,
/// where the compatibility layer's has been uneven; and the counts come back
/// under names of their own, so an adapter pretending to be the OpenAI one
/// reported zero usage for every call.
/// </para>
/// <para>
/// The client is an <see cref="IChatClient"/> of its own accord, so composition
/// here reads the same as it does for OpenAI. Drawing does not: no model this
/// runner serves makes pictures, and the settings screen does not offer the
/// job — this is the backstop for a request that arrived anyway.
/// </para>
/// <para>
/// No key: there is nobody to authenticate to. The address is not optional for
/// the same reason — a model on your own machine is wherever you put it.
/// </para>
/// </remarks>
/// <param name="http">The shared client.</param>
/// <param name="logger">Records what the runner refused, and why.</param>
internal sealed class OllamaAssistant(
    AssistantHttp http,
    ILogger<OllamaAssistant> logger) : IAssistant
{
    /// <summary>Where Ollama listens when nobody has moved it.</summary>
    /// <remarks>
    /// Only a hint for the settings screen. The address is required, because a
    /// default that is wrong on the machines where it matters is worse than an
    /// empty box.
    /// </remarks>
    internal const string UsualAddress = "http://localhost:11434";

    public AssistantKind Kind => AssistantKind.Ollama;

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
            using var transport = http.ClientFor(@using.BaseUrl);
            using var client = new OllamaApiClient(transport, @using.Model);

            // Through the interface on purpose: the client offers a shape of
            // its own beside this one, and what this adapter wants is the one
            // the OpenAI adapter also speaks.
            var answered = await ((IChatClient)client)
                .GetResponseAsync(conversation, options, cancellationToken)
                .ConfigureAwait(false);

            return Read(answered);
        }
        catch (Exception failure) when (Expected(failure))
        {
            return Failure(failure);
        }
    }

    /// <summary>
    /// Refused, because nothing this runner serves draws.
    /// </summary>
    /// <remarks>
    /// A fact about the provider rather than a failure of the call, which is
    /// why the settings screen declines to offer the switch at all. This is the
    /// backstop for a request that arrived anyway.
    /// </remarks>
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
                    .Select(model => model.Name ?? string.Empty)
                    .Where(name => name.Length > 0)
                    // Whether anything here draws is read from its name like
                    // everywhere else. Ollama serves language and vision models
                    // today, so this is expected to say no to all of them — and
                    // says yes rather than hiding a model somebody has pulled
                    // on purpose.
                    .Select(name => new ModelInfo(name, name, DrawingModel.Draws(name)))
                    .OrderBy(model => model.Id, StringComparer.Ordinal)
            ];

            return Result<IReadOnlyList<ModelInfo>>.Success(offered);
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
            // A local model held to a schema still occasionally answers with
            // something else. Ordinary rather than exceptional, and the person
            // asking gets to try again.
            return AssistanceErrors.UnusableAnswer;
        }
    }

    private static ModelUsage Usage(ChatResponse answered) => new(
        (int)(answered.Usage?.InputTokenCount ?? 0),
        (int)(answered.Usage?.OutputTokenCount ?? 0),
        Pictures: 0);

    /// <summary>The failures that are the runner's rather than this app's.</summary>
    private static bool Expected(Exception failure) =>
        failure is HttpRequestException or OperationCanceledException or IOException
            or InvalidOperationException or JsonException;

    /// <summary>
    /// What a refusal means.
    /// </summary>
    /// <remarks>
    /// No key here, so nothing can be rejected for one. What is left is a
    /// runner that is not there, is busy, or was asked for a model it has not
    /// pulled.
    /// </remarks>
    private static Error Failure(Exception failure) =>
        failure is HttpRequestException { StatusCode: { } status }
            ? status switch
            {
                System.Net.HttpStatusCode.TooManyRequests => AssistanceErrors.Throttled,
                System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadRequest =>
                    AssistanceErrors.Refused,
                _ => AssistanceErrors.Unavailable
            }
            : AssistanceErrors.Unavailable;
}
