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
/// The second half of that sentence is why the base address is configurable. A
/// great many self-hosted model runners speak this API and nothing else, so an
/// adapter for it is also the adapter for a model on the machine next door.
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
    private const string Home = "https://api.openai.com";

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
            model = settings.ComposeModel,
            messages = new object[]
            {
                new { role = "system", content = request.Instruction },
                new { role = "user", content = Content(request) }
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = RecipeSchema.Name,
                    schema = RecipeSchema.Definition
                }
            }
        };

        var answered = await http.PostAsync<OpenAiReply>(
                AssistantHttp.Address(settings.BaseUrl, Home, "v1/chat/completions"),
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
            model = settings.DrawModel,
            prompt = request.Subject,
            n = 1,
            // One square picture at the middling quality. Not a choice this app
            // offers, because every other combination costs more and none of
            // them makes a recipe card look different.
            size = "1024x1024"
        };

        var answered = await http.PostAsync<OpenAiImageReply>(
                AssistantHttp.Address(settings.BaseUrl, Home, "v1/images/generations"),
                payload,
                message => Authorize(message, key),
                cancellationToken)
            .ConfigureAwait(false);

        return answered.Bind(ReadPicture);
    }

    private static void Authorize(HttpRequestMessage message, string key) =>
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

    /// <summary>
    /// The material, as content parts, with the instruction nowhere near it.
    /// </summary>
    /// <remarks>
    /// The instruction is the system message. This builds only the untrusted
    /// half — what somebody pasted, typed or photographed — and the two are
    /// never concatenated.
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
                type = "image_url",
                image_url = new
                {
                    url = $"data:{media};base64,{Convert.ToBase64String(request.Picture.Span)}"
                }
            });
        }

        return [.. parts];
    }

    private Result<Composed> Read(OpenAiReply reply)
    {
        if (reply.Choices is not [{ Message.Content: { Length: > 0 } json }, ..])
        {
            AssistanceLogs.EmptyAnswer(
                logger,
                Kind.Code,
                reply.Choices is [var refused, ..] ? refused.FinishReason : null);

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
        reply.Usage?.PromptTokens ?? 0,
        reply.Usage?.CompletionTokens ?? 0,
        Pictures: 0);

    /// <inheritdoc cref="GeminiAssistant" />
    private string? Key() =>
        settings.HasApiKey ? protector.Unprotect(settings.ProtectedApiKey) : null;
}

/// <summary>What a chat completion answers with.</summary>
internal sealed record OpenAiReply
{
    public IReadOnlyList<OpenAiChoice>? Choices { get; init; }

    public OpenAiUsage? Usage { get; init; }
}

/// <summary>One answer.</summary>
internal sealed record OpenAiChoice
{
    public OpenAiMessage? Message { get; init; }

    public string? FinishReason { get; init; }
}

/// <summary>The answer's text.</summary>
internal sealed record OpenAiMessage
{
    public string? Content { get; init; }
}

/// <summary>What the call consumed, as OpenAI counts it.</summary>
internal sealed record OpenAiUsage
{
    public int PromptTokens { get; init; }

    public int CompletionTokens { get; init; }

    /// <summary>What an image call reports instead.</summary>
    public int InputTokens { get; init; }

    /// <summary>What an image call reports instead.</summary>
    public int OutputTokens { get; init; }
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
