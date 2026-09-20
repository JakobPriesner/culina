using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Abstractions;
using Application.Assistance;
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
/// Stateless with respect to configuration: the key, the address and the model
/// all arrive with the call. One instance therefore serves however many
/// connections and however many models an administrator has set up.
/// </para>
/// </remarks>
/// <param name="http">The shared client.</param>
/// <param name="logger">Records what a provider refused, and why.</param>
internal sealed class GeminiAssistant(
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
        Connected @using,
        Composition request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);
        ArgumentNullException.ThrowIfNull(request);

        var payload = new
        {
            model = @using.Model,
            input = Input(request),
            response_format = new
            {
                type = "text",
                mime_type = "application/json",
                schema = RecipeSchema.Definition
            }
        };

        var answered = await PostAsync(@using, payload, cancellationToken).ConfigureAwait(false);

        return answered.Bind(Read);
    }

    public async Task<Result<Drawn>> DrawAsync(
        Connected @using,
        Drawing request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);
        ArgumentNullException.ThrowIfNull(request);

        // The same endpoint. An image model answers with an image part where a
        // text model answers with a text one, which is the point of the steps
        // being typed.
        var payload = new
        {
            model = @using.Model,
            input = new object[] { new { type = "text", text = request.Subject } }
        };

        var answered = await PostAsync(@using, payload, cancellationToken).ConfigureAwait(false);

        return answered.Bind(ReadPicture);
    }

    public async Task<Result<IReadOnlyList<ModelInfo>>> ListModelsAsync(
        Connected @using,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);

        var listed = await http.GetAsync<GeminiModelList>(
                AssistantHttp.Address(@using.BaseUrl, "v1beta/models"),
                message =>
                {
                    message.Headers.Add("x-goog-api-key", @using.ApiKey);
                    message.Headers.Add("Api-Revision", Revision);
                },
                cancellationToken)
            .ConfigureAwait(false);

        return listed.Map(list => ModelLabels.Distinguish(
        [
            .. (list.Models ?? [])
                .Where(Generative)
                .Select(model => new ModelInfo(Named(model), Labelled(model), Draws(model)))
                .OrderBy(model => model.Id, StringComparer.Ordinal)
        ]));
    }

    /// <summary>
    /// Whether this is a model that makes something, rather than one that
    /// measures.
    /// </summary>
    /// <remarks>
    /// The list carries embedding and token-counting models too, and offering
    /// those as a choice for "write me a recipe" would be offering something
    /// that cannot answer.
    /// </remarks>
    private static bool Generative(GeminiModel model) =>
        model.SupportedGenerationMethods is null
        || model.SupportedGenerationMethods.Any(method =>
            method.Contains("generate", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether it draws.
    /// </summary>
    /// <remarks>
    /// Read from the name, because nothing in the listing says so plainly — and
    /// from the display name too, because that is where "Nano Banana" is
    /// written. <c>predict</c> is the one thing the listing does say: it is how
    /// the Imagen family is served, and nothing that writes text uses it.
    /// </remarks>
    private static bool Draws(GeminiModel model) =>
        DrawingModel.Draws(Named(model), model.DisplayName)
        || (model.SupportedGenerationMethods ?? []).Any(method =>
            method.StartsWith("predict", StringComparison.OrdinalIgnoreCase));

    /// <summary>The id to send, without the <c>models/</c> the listing prefixes.</summary>
    private static string Named(GeminiModel model) =>
        (model.Name ?? string.Empty).StartsWith("models/", StringComparison.Ordinal)
            ? model.Name![7..]
            : model.Name ?? string.Empty;

    private static string Labelled(GeminiModel model) =>
        string.IsNullOrWhiteSpace(model.DisplayName) ? Named(model) : model.DisplayName;

    private Task<Result<GeminiReply>> PostAsync(
        Connected @using,
        object payload,
        CancellationToken cancellationToken) =>
        http.PostAsync<GeminiReply>(
            AssistantHttp.Address(@using.BaseUrl, "v1beta/interactions"),
            payload,
            message =>
            {
                message.Headers.Add("x-goog-api-key", @using.ApiKey);
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
        reply.Usage?.InputTokens ?? 0,
        reply.Usage?.OutputTokens ?? 0,
        pictures);

}

/// <summary>What the models listing answers with.</summary>
internal sealed record GeminiModelList
{
    public IReadOnlyList<GeminiModel>? Models { get; init; }
}

/// <summary>One model Google offers.</summary>
internal sealed record GeminiModel
{
    /// <summary>Prefixed <c>models/</c> in the listing, not in a request.</summary>
    public string? Name { get; init; }

    public string? DisplayName { get; init; }

    public IReadOnlyList<string>? SupportedGenerationMethods { get; init; }
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

/// <summary>
/// What the call consumed.
/// </summary>
/// <remarks>
/// <para>
/// Named on every property. The Interactions API accounts for a whole turn
/// rather than for one message, so the fields are <c>total_input_tokens</c> and
/// <c>total_output_tokens</c> — and they are snake_case, which the shared
/// options do not bridge. Under the old names they bound to zero, which is what
/// every row in the ledger says this instance has spent.
/// </para>
/// <para>
/// Thinking is counted separately in <c>total_thought_tokens</c> and is not
/// included here. Google bills it, so a budget built from these two runs
/// slightly under the invoice on a reasoning model — the alternative is to add
/// a number to output that the documentation does not say belongs there.
/// </para>
/// </remarks>
internal sealed record GeminiUsage
{
    [JsonPropertyName("total_input_tokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("total_output_tokens")]
    public int OutputTokens { get; init; }

    [JsonPropertyName("total_thought_tokens")]
    public int ThoughtTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}
