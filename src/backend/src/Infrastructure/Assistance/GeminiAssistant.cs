using System.Text.Json;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Assistance;
using Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Assistance;

/// <summary>
/// Talks to Google's Gemini models, over the Interactions API.
/// </summary>
/// <remarks>
/// <para>
/// The Interactions API rather than <c>generateContent</c>, which Google now
/// calls the legacy generate-content API. It is pinned to a revision with the
/// <c>Api-Revision</c> header, which is the whole reason that header exists:
/// the shape changed under everybody in May 2026 — <c>steps</c> replaced
/// <c>outputs</c>, and <c>response_format</c> absorbed
/// <c>response_mime_type</c> — and an unpinned client is one that will do that
/// again without warning.
/// </para>
/// <para>
/// One thing is genuinely weaker here than with the other two providers, and it
/// is worth naming rather than hiding: Interactions has no separate system-
/// instruction field, so the instruction and the untrusted material travel as
/// two <em>content parts</em> of one input rather than as two different kinds of
/// message. They are still never concatenated, and the instruction is still
/// always first, but a model that decides to read part two as an instruction has
/// less standing in its way than OpenAI's system role gives.
/// </para>
/// <para>
/// The settings are read per call rather than captured in the constructor: they
/// are a mutable singleton an administrator can change without a restart, and an
/// adapter holding the key it was built with would keep using a rotated one.
/// </para>
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
    /// <summary>
    /// The shape of the API this was written against.
    /// </summary>
    /// <remarks>
    /// Sent on every request. Raising it is a deliberate act with a changelog to
    /// read first, which is exactly what it should be.
    /// </remarks>
    private const string Revision = "2026-05-20";

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
            model = settings.ComposeModel.Or(AssistantDefaults.ComposeModel(Kind)),
            input = Input(request),
            response_format = new
            {
                type = "text",
                mime_type = "application/json",
                schema = RecipeSchema.Definition
            }
        };

        var answered = await PostAsync(payload, key, cancellationToken).ConfigureAwait(false);

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

        // The same endpoint. An image model answers with an image part where a
        // text model answers with a text one, which is the point of the steps
        // being typed.
        var payload = new
        {
            model = settings.DrawModel.Or(AssistantDefaults.DrawModel(Kind)),
            input = new object[] { new { type = "text", text = request.Subject } }
        };

        var answered = await PostAsync(payload, key, cancellationToken).ConfigureAwait(false);

        return answered.Bind(ReadPicture);
    }

    private Task<Result<GeminiReply>> PostAsync(
        object payload,
        string key,
        CancellationToken cancellationToken) =>
        http.PostAsync<GeminiReply>(
            AssistantHttp.Address(settings.BaseUrl, AssistantDefaults.Home(Kind), "v1beta/interactions"),
            payload,
            message =>
            {
                message.Headers.Add("x-goog-api-key", key);
                message.Headers.Add("Api-Revision", Revision);
            },
            cancellationToken);

    /// <summary>
    /// The instruction and the material, as separate parts of one input.
    /// </summary>
    /// <remarks>
    /// Never joined into one string, and the instruction is always first. See
    /// the note on this class: two parts is what this API offers in place of a
    /// system role, and it is weaker.
    /// </remarks>
    private static object[] Input(Composition request)
    {
        List<object> parts = [new { type = "text", text = request.Instruction }];

        if (request.Material is { Length: > 0 } material)
        {
            parts.Add(new { type = "text", text = material });
        }

        if (!request.Picture.IsEmpty)
        {
            parts.Add(new
            {
                type = "image",
                mime_type = request.PictureMediaType ?? "image/jpeg",
                data = Convert.ToBase64String(request.Picture.Span)
            });
        }

        return [.. parts];
    }

    private Result<Composed> Read(GeminiReply reply)
    {
        if (Part(reply, "text")?.Text is not { Length: > 0 } json)
        {
            AssistanceLogs.EmptyAnswer(logger, Kind.Code, reply.Status);

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
        if (Part(reply, "image")?.Data is not { Length: > 0 } data)
        {
            AssistanceLogs.EmptyAnswer(logger, Kind.Code, reply.Status);

            return AssistanceErrors.UnusableAnswer;
        }

        return new Drawn(
            new MemoryStream(Convert.FromBase64String(data)),
            Usage(reply, pictures: 1));
    }

    /// <summary>
    /// The first part of that type the model produced.
    /// </summary>
    /// <remarks>
    /// Only <c>model_output</c> steps. A timeline can also carry the input back
    /// and, once tools are in play, function calls — and reading the echo of
    /// what was sent as if it were the answer is the kind of mistake that looks
    /// like it works.
    /// </remarks>
    private static GeminiPart? Part(GeminiReply reply, string type) => reply.Steps?
        .Where(step => step.Type == "model_output")
        .SelectMany(step => step.Content ?? [])
        .FirstOrDefault(part => part.Type == type);

    private static ModelUsage Usage(GeminiReply reply, int pictures) => new(
        reply.Usage?.PromptTokens ?? 0,
        reply.Usage?.CompletionTokens ?? 0,
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

/// <summary>What the Interactions API answers with.</summary>
internal sealed record GeminiReply
{
    public IReadOnlyList<GeminiStep>? Steps { get; init; }

    /// <summary>Set when the interaction wants something before it can finish.</summary>
    public string? Status { get; init; }

    public GeminiUsage? Usage { get; init; }
}

/// <summary>One step of the interaction.</summary>
internal sealed record GeminiStep
{
    /// <summary><c>model_output</c>, <c>user_input</c>, <c>function_call</c>.</summary>
    public string? Type { get; init; }

    public IReadOnlyList<GeminiPart>? Content { get; init; }
}

/// <summary>One piece of a step: words, or a picture.</summary>
internal sealed record GeminiPart
{
    /// <summary><c>text</c> or <c>image</c>.</summary>
    public string? Type { get; init; }

    public string? Text { get; init; }

    public string? MimeType { get; init; }

    /// <summary>A picture, base64-encoded.</summary>
    public string? Data { get; init; }
}

/// <summary>What the call consumed.</summary>
internal sealed record GeminiUsage
{
    public int PromptTokens { get; init; }

    public int CompletionTokens { get; init; }

    public int TotalTokens { get; init; }
}
