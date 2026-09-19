using System.Net.Http.Headers;
using System.Text.Json;
using Application.Abstractions;
using Application.Abstractions.Settings;
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
/// </remarks>
/// <param name="settings">The live instance settings.</param>
/// <param name="protector">Decrypts the stored key.</param>
/// <param name="http">The shared client.</param>
/// <param name="logger">Records what a provider refused, and why.</param>
internal sealed class OpenAiAssistant(
    AssistanceSettings settings,
    ISecretProtector protector,
    AssistantHttp http,
    ILogger<OpenAiAssistant> logger) : IAssistant
{
    public AssistantKind Kind => AssistantKind.OpenAi;

    public async Task<Result<Composed>> ComposeAsync(
        Composition request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Key() is not { } key)
        {
            return AssistanceErrors.NotConfigured;
        }

        var payload = new
        {
            model = settings.ComposeModel.Or(AssistantDefaults.ComposeModel(Kind)),
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
                AssistantHttp.Address(settings.BaseUrl, AssistantDefaults.Home(Kind), "v1/responses"),
                payload,
                message => Authorize(message, key),
                cancellationToken)
            .ConfigureAwait(false);

        return answered.Bind(Read);
    }

    public async Task<Result<Drawn>> DrawAsync(
        Drawing request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Key() is not { } key)
        {
            return AssistanceErrors.NotConfigured;
        }

        var payload = new
        {
            model = settings.DrawModel.Or(AssistantDefaults.DrawModel(Kind)),
            prompt = request.Subject,
            n = 1,
            // One square picture at the middling quality. Not a choice this app
            // offers, because every other combination costs more and none of
            // them makes a recipe card look different.
            size = "1024x1024"
        };

        var answered = await http.PostAsync<OpenAiImageReply>(
                AssistantHttp.Address(
                    settings.BaseUrl,
                    AssistantDefaults.Home(Kind),
                    "v1/images/generations"),
                payload,
                message => Authorize(message, key),
                cancellationToken)
            .ConfigureAwait(false);

        return answered.Bind(ReadPicture);
    }

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

    /// <inheritdoc cref="GeminiAssistant" />
    private string? Key() =>
        settings.HasApiKey ? protector.Unprotect(settings.ProtectedApiKey) : null;
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
