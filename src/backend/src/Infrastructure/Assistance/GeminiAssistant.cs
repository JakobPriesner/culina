using System.Text.Json;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Assistance;
using Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Assistance;

/// <summary>Talks to Google's Gemini models.</summary>
/// <remarks>
/// The settings are read per call rather than captured in the constructor: they
/// are a mutable singleton an administrator can change without a restart, and
/// an adapter holding the key it was built with would keep using a key that had
/// been rotated.
/// </remarks>
/// <param name="settings">The live instance settings.</param>
/// <param name="protector">Decrypts the stored key.</param>
/// <param name="http">The shared client.</param>
/// <param name="logger">Records what a provider refused, and why.</param>
internal sealed class GeminiAssistant(
    AssistanceSettings settings,
    ISecretProtector protector,
    AssistantHttp http,
    ILogger<GeminiAssistant> logger) : IAssistant
{
    private const string Home = "https://generativelanguage.googleapis.com";

    public AssistantKind Kind => AssistantKind.Gemini;

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
            systemInstruction = new { parts = new[] { new { text = request.Instruction } } },
            contents = new[] { new { role = "user", parts = Parts(request) } },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = RecipeSchema.Definition
            }
        };

        var answered = await http.PostAsync<GeminiReply>(
                AssistantHttp.Address(
                    settings.BaseUrl,
                    Home,
                    $"v1beta/models/{settings.ComposeModel}:generateContent"),
                payload,
                message => message.Headers.Add("x-goog-api-key", key),
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
            contents = new[]
            {
                new { role = "user", parts = new object[] { new { text = request.Subject } } }
            },
            generationConfig = new { responseModalities = new[] { "IMAGE" } }
        };

        var answered = await http.PostAsync<GeminiReply>(
                AssistantHttp.Address(
                    settings.BaseUrl,
                    Home,
                    $"v1beta/models/{settings.DrawModel}:generateContent"),
                payload,
                message => message.Headers.Add("x-goog-api-key", key),
                cancellationToken)
            .ConfigureAwait(false);

        return answered.Bind(ReadPicture);
    }

    /// <summary>
    /// The instruction and the material, in different parts of the request.
    /// </summary>
    /// <remarks>
    /// The instruction is in <c>systemInstruction</c> and never here. What this
    /// builds is only ever the untrusted half: what somebody pasted, typed or
    /// photographed. They are never concatenated, which is the whole defence.
    /// </remarks>
    private static object[] Parts(Composition request)
    {
        List<object> parts = [];

        if (request.Material is { Length: > 0 } material)
        {
            parts.Add(new { text = material });
        }

        if (!request.Picture.IsEmpty)
        {
            parts.Add(new
            {
                inlineData = new
                {
                    mimeType = request.PictureMediaType ?? "image/jpeg",
                    data = Convert.ToBase64String(request.Picture.Span)
                }
            });
        }

        return [.. parts];
    }

    private Result<Composed> Read(GeminiReply reply)
    {
        if (Text(reply) is not { Length: > 0 } json)
        {
            AssistanceLogs.EmptyAnswer(logger, Kind.Code, reply.Candidates is [var refused, ..] ? refused.FinishReason : null);

            return AssistanceErrors.UnusableAnswer;
        }

        try
        {
            var answer = JsonSerializer.Deserialize<RecipeAnswer>(json, AssistantHttp.Json);

            return answer is null
                ? AssistanceErrors.UnusableAnswer
                : new Composed(answer.ToDraft(), Usage(reply, pictures: 0));
        }
        catch (JsonException)
        {
            // The model answered with something that is not the shape it was
            // given. Ordinary rather than exceptional, and the person asking
            // gets to try again.
            return AssistanceErrors.UnusableAnswer;
        }
    }

    private Result<Drawn> ReadPicture(GeminiReply reply)
    {
        var data = reply.Candidates?
            .SelectMany(candidate => candidate.Content?.Parts ?? [])
            .Select(part => part.InlineData?.Data)
            .FirstOrDefault(value => value is { Length: > 0 });

        if (data is null)
        {
            AssistanceLogs.EmptyAnswer(logger, Kind.Code, reply.Candidates is [var refused, ..] ? refused.FinishReason : null);

            return AssistanceErrors.UnusableAnswer;
        }

        return new Drawn(
            new MemoryStream(Convert.FromBase64String(data)),
            Usage(reply, pictures: 1));
    }

    private static string? Text(GeminiReply reply) => reply.Candidates?
        .SelectMany(candidate => candidate.Content?.Parts ?? [])
        .Select(part => part.Text)
        .FirstOrDefault(value => value is { Length: > 0 });

    private static ModelUsage Usage(GeminiReply reply, int pictures) => new(
        reply.UsageMetadata?.PromptTokenCount ?? 0,
        reply.UsageMetadata?.CandidatesTokenCount ?? 0,
        pictures);

    /// <summary>
    /// The key, decrypted, or null when there is not a usable one.
    /// </summary>
    /// <remarks>
    /// Null covers two cases that are the same thing from here: nobody entered
    /// a key, and the key ring that encrypted one was lost. Both are "this
    /// instance has no assistant", and both are fixed by entering it again.
    /// </remarks>
    private string? Key() =>
        settings.HasApiKey ? protector.Unprotect(settings.ProtectedApiKey) : null;
}

/// <summary>What Gemini answers with.</summary>
internal sealed record GeminiReply
{
    public IReadOnlyList<GeminiCandidate>? Candidates { get; init; }

    public GeminiUsage? UsageMetadata { get; init; }
}

/// <summary>One answer.</summary>
internal sealed record GeminiCandidate
{
    public GeminiContent? Content { get; init; }

    public string? FinishReason { get; init; }
}

/// <summary>The parts of one answer.</summary>
internal sealed record GeminiContent
{
    public IReadOnlyList<GeminiPart>? Parts { get; init; }
}

/// <summary>One part: words, or a picture.</summary>
internal sealed record GeminiPart
{
    public string? Text { get; init; }

    public GeminiInlineData? InlineData { get; init; }
}

/// <summary>A picture, base64-encoded.</summary>
internal sealed record GeminiInlineData
{
    public string? MimeType { get; init; }

    public string? Data { get; init; }
}

/// <summary>What the call consumed, as Gemini counts it.</summary>
internal sealed record GeminiUsage
{
    public int PromptTokenCount { get; init; }

    public int CandidatesTokenCount { get; init; }
}
