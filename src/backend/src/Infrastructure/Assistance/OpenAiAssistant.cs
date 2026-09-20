using System.Net.Http.Headers;
using System.Text.Json;
using Application.Abstractions;
using Domain.Assistance;
using Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Assistance;

/// <summary>
/// Talks to OpenAI's models, and to anything that answers in their shape.
/// </summary>
/// <remarks>
/// <para>
/// The Responses API rather than chat completions, which is what OpenAI now
/// points new work at. Structured output moved with it: what was
/// <c>response_format</c> at the top level is <c>text.format</c> here.
/// </para>
/// <para>
/// The system role survives the move, which matters more to this feature than
/// anything else about it. The instruction is a <c>system</c> message and the
/// untrusted material is a <c>user</c> message — two different kinds of thing
/// rather than two parts of one, which is the strongest separation any of the
/// three providers offers.
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
/// <param name="http">The shared client.</param>
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

        var payload = new
        {
            model = @using.Model,
            input = new object[]
            {
                new { role = "system", content = request.Instruction },
                new { role = "user", content = Content(request) }
            },
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = RecipeSchema.Name,
                    schema = RecipeSchema.Definition
                }
            }
        };

        var answered = await http.PostAsync<OpenAiReply>(
                AssistantHttp.Address(@using.BaseUrl, "v1/responses"),
                payload,
                message => Authorize(message, @using.ApiKey),
                cancellationToken)
            .ConfigureAwait(false);

        return answered.Bind(Read);
    }

    public async Task<Result<Drawn>> DrawAsync(
        Connected @using,
        Drawing request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);
        ArgumentNullException.ThrowIfNull(request);

        var payload = new
        {
            model = @using.Model,
            prompt = request.Subject,
            n = 1,
            // One square picture at the middling quality. Not a choice this app
            // offers, because every other combination costs more and none of
            // them makes a recipe card look different.
            size = "1024x1024"
        };

        var answered = await http.PostAsync<OpenAiImageReply>(
                AssistantHttp.Address(@using.BaseUrl, "v1/images/generations"),
                payload,
                message => Authorize(message, @using.ApiKey),
                cancellationToken)
            .ConfigureAwait(false);

        return answered.Bind(ReadPicture);
    }

    public async Task<Result<IReadOnlyList<ModelInfo>>> ListModelsAsync(
        Connected @using,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);

        var listed = await http.GetAsync<OpenAiModelList>(
                AssistantHttp.Address(@using.BaseUrl, "v1/models"),
                message => Authorize(message, @using.ApiKey),
                cancellationToken)
            .ConfigureAwait(false);

        return listed.Map(list => (IReadOnlyList<ModelInfo>)
        [
            .. (list.Data ?? [])
                .Select(model => model.Id ?? string.Empty)
                .Where(Usable)
                .Select(id => new ModelInfo(id, id, DrawingModel.Draws(id)))
                .OrderBy(model => model.Id, StringComparer.Ordinal)
        ]);
    }

    /// <summary>
    /// What is left after the models that cannot write a recipe.
    /// </summary>
    /// <remarks>
    /// This listing is everything the account can reach — embeddings, speech,
    /// transcription, moderation, the lot — and there is no field saying which
    /// is which. Filtering by name is crude and is the only thing available;
    /// erring towards keeping a model means an odd entry in a list, where
    /// erring the other way means a model somebody wanted is missing.
    /// </remarks>
    private static bool Usable(string id) =>
        id.Length > 0
        && !id.Contains("embedding", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("moderation", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("whisper", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("tts", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("audio", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("realtime", StringComparison.OrdinalIgnoreCase);


    private static void Authorize(HttpRequestMessage message, string key) =>
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

    /// <summary>
    /// The material, as content parts of the user message.
    /// </summary>
    /// <remarks>
    /// The instruction is the system message and is never here. This builds only
    /// the untrusted half — what somebody pasted, typed or photographed — and
    /// the two are never concatenated.
    /// </remarks>
    private static object[] Content(Composition request)
    {
        List<object> parts = [];

        if (request.Material is { Length: > 0 } material)
        {
            parts.Add(new { type = "text", text = material });
        }

        if (!request.Picture.IsEmpty)
        {
            var media = request.PictureMediaType ?? "image/jpeg";

            parts.Add(new
            {
                type = "image",
                image = new
                {
                    url = $"data:{media};base64,{Convert.ToBase64String(request.Picture.Span)}"
                }
            });
        }

        return [.. parts];
    }

    private Result<Composed> Read(OpenAiReply reply)
    {
        if (Text(reply) is not { Length: > 0 } json)
        {
            AssistanceLogs.EmptyAnswer(logger, Kind.Code, reply.Status);

            return AssistanceErrors.UnusableAnswer;
        }

        try
        {
            var answer = JsonSerializer.Deserialize<RecipeAnswer>(json, AssistantHttp.Json);

            return answer is null
                ? AssistanceErrors.UnusableAnswer
                : new Composed(answer.ToDraft(), Usage(reply));
        }
        catch (JsonException)
        {
            return AssistanceErrors.UnusableAnswer;
        }
    }

    /// <summary>
    /// The first piece of text the model produced.
    /// </summary>
    /// <remarks>
    /// Searched rather than indexed at <c>output[0].content[0]</c>. A response
    /// can carry reasoning items and tool calls alongside the message, and the
    /// answer is not reliably the first thing in the list.
    /// </remarks>
    private static string? Text(OpenAiReply reply) => reply.Output?
        .SelectMany(item => item.Content ?? [])
        .FirstOrDefault(part => part.Text is { Length: > 0 })?
        .Text;

    private Result<Drawn> ReadPicture(OpenAiImageReply reply)
    {
        if (reply.Data is not [{ B64Json: { Length: > 0 } data }, ..])
        {
            AssistanceLogs.EmptyAnswer(logger, Kind.Code, finishReason: null);

            return AssistanceErrors.UnusableAnswer;
        }

        return new Drawn(
            new MemoryStream(Convert.FromBase64String(data)),
            new ModelUsage(
                reply.Usage?.InputTokens ?? 0,
                reply.Usage?.OutputTokens ?? 0,
                Pictures: 1));
    }

    private static ModelUsage Usage(OpenAiReply reply) => new(
        reply.Usage?.InputTokens ?? 0,
        reply.Usage?.OutputTokens ?? 0,
        Pictures: 0);

}

/// <summary>What the models listing answers with.</summary>
internal sealed record OpenAiModelList
{
    public IReadOnlyList<OpenAiModel>? Data { get; init; }
}

/// <summary>One model the account can reach.</summary>
internal sealed record OpenAiModel
{
    public string? Id { get; init; }
}

/// <summary>What the Responses API answers with.</summary>
internal sealed record OpenAiReply
{
    public IReadOnlyList<OpenAiOutputItem>? Output { get; init; }

    /// <summary>Set when the response did not simply complete.</summary>
    public string? Status { get; init; }

    public OpenAiUsage? Usage { get; init; }
}

/// <summary>One item of the output: a message, a reasoning block, a tool call.</summary>
internal sealed record OpenAiOutputItem
{
    public string? Type { get; init; }

    public IReadOnlyList<OpenAiContentPart>? Content { get; init; }
}

/// <summary>One piece of an output item.</summary>
internal sealed record OpenAiContentPart
{
    public string? Type { get; init; }

    public string? Text { get; init; }
}

/// <summary>What the call consumed.</summary>
internal sealed record OpenAiUsage
{
    public int InputTokens { get; init; }

    public int OutputTokens { get; init; }

    public int TotalTokens { get; init; }
}

/// <summary>What an image generation answers with.</summary>
internal sealed record OpenAiImageReply
{
    public IReadOnlyList<OpenAiImage>? Data { get; init; }

    public OpenAiUsage? Usage { get; init; }
}

/// <summary>One generated picture.</summary>
internal sealed record OpenAiImage
{
    public string? B64Json { get; init; }
}
