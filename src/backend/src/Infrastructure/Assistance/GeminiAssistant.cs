using System.Runtime.CompilerServices;
using System.Text.Json;
using Application.Abstractions;
using Application.Assistance;
using Domain.Assistance;
using Domain.Shared;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Assistance;

/// <summary>Talks to Google's models through Google's own client library.</summary>
/// <remarks>
/// The SDK reports the token counts the hand-written client read from fields nothing sends, which
/// recorded every call as free. It has no <c>Microsoft.Extensions.AI</c> package, so this adapter
/// speaks it directly. One call both writes (text) and draws (inline image); the key, address and
/// model arrive with each call.
/// </remarks>
/// <param name="http">The shared client, whose rules the SDK is made to keep.</param>
/// <param name="logger">Records what a provider refused, and why.</param>
internal sealed class GeminiAssistant(
    AssistantHttp http,
    ILogger<GeminiAssistant> logger) : IAssistant
{
    public AssistantKind Kind => AssistantKind.Gemini;

    public async Task<Result<Composed>> ComposeAsync(
        Connected @using,
        Composition request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var client = Client(@using);

            var answered = await client.Models
                .GenerateContentAsync(@using.Model, Material(request), Writing(request), cancellationToken)
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

        var answer = new PartialRecipe();
        var usage = default(ModelUsage);
        string? finishReason = null;

        using var client = Client(@using);

        // Advanced by hand: a `yield` may not sit in a `try` that catches, and everything that
        // talks to the provider must.
        var parts = client.Models
            .GenerateContentStreamAsync(@using.Model, Material(request), Writing(request), cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                GenerateContentResponse? part = null;
                Exception? thrown = null;

                try
                {
                    if (await parts.MoveNextAsync().ConfigureAwait(false))
                    {
                        part = parts.Current;
                    }
                }
                catch (Exception failure) when (Expected(failure))
                {
                    thrown = failure;
                }

                if (thrown is not null)
                {
                    yield return Stopped(answer, Failed(thrown));

                    yield break;
                }

                if (part is null)
                {
                    break;
                }

                answer.Add(part.Text);
                usage = Counted(part, usage);
                finishReason ??= Reason(part);

                if (answer.Read() is { } soFar)
                {
                    yield return new Composing { Recipe = soFar };
                }
            }
        }
        finally
        {
            await parts.DisposeAsync().ConfigureAwait(false);
        }

        if (answer.ReadWhole() is not { } written)
        {
            AssistanceLogs.EmptyAnswer(logger, Kind.Code, finishReason);

            yield return Stopped(answer, AssistanceErrors.UnusableAnswer);

            yield break;
        }

        yield return new Composing { Recipe = written, Finished = true, Usage = usage };
    }

    public async Task<Result<Drawn>> DrawAsync(
        Connected @using,
        Drawing request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);
        ArgumentNullException.ThrowIfNull(request);

        var config = new GenerateContentConfig
        {
            // Asked for explicitly: an image model given no modalities describes the picture
            // instead of drawing it.
            ResponseModalities = ["TEXT", "IMAGE"]
        };

        try
        {
            using var client = Client(@using, drawing: true);

            var answered = await client.Models
                .GenerateContentAsync(@using.Model, request.Subject, config, cancellationToken)
                .ConfigureAwait(false);

            return ReadPicture(answered);
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
            List<ModelInfo> listed = [];

            using var client = Client(@using);

            var pages = await client.Models
                .ListAsync(new ListModelsConfig { PageSize = 100 }, cancellationToken)
                .ConfigureAwait(false);

            await foreach (var model in pages.ConfigureAwait(false))
            {
                if (Named(model) is not { Length: > 0 } id)
                {
                    continue;
                }

                listed.Add(new ModelInfo(id, Labelled(model, id), DrawingModel.Draws(id, model.DisplayName)));
            }

            // Name order, no date: Google's listing has none, and an invented date would sort
            // convincingly and wrongly.
            IReadOnlyList<ModelInfo> everything = ModelLabels.Distinguish(
                [.. listed.OrderBy(model => model.Id, StringComparer.Ordinal)]);

            return Result<IReadOnlyList<ModelInfo>>.Success(
                ModelCatalogue.Narrow(everything, model => Usable(model.Id)));
        }
        catch (Exception failure) when (Expected(failure))
        {
            return Failed(failure);
        }
    }

    /// <summary>How to ask for a recipe, whether or not it is read as it arrives.</summary>
    private static GenerateContentConfig Writing(Composition request) => new()
    {
        // The instruction is a field of its own, not a turn: the strongest separation from what a
        // stranger pasted.
        SystemInstruction = new Content { Parts = [new Part { Text = request.Instruction }] },
        ResponseMimeType = "application/json",
        ResponseJsonSchema = RecipeSchema.Definition,
        MaxOutputTokens = Composition.MostOutputTokens
    };

    /// <summary>
    /// The last part of a stream that ended badly; it still carries the recipe so the screen can
    /// offer it beside the reason.
    /// </summary>
    private static Composing Stopped(PartialRecipe answer, Error failure) => new()
    {
        Recipe = answer.SoFar(),
        Finished = true,
        Failure = failure
    };

    /// <summary>
    /// The usage counts, kept when a chunk omits them: a call recorded as free is the fault this
    /// SDK was adopted to remove.
    /// </summary>
    private static ModelUsage Counted(GenerateContentResponse part, ModelUsage soFar) =>
        part.UsageMetadata is null
            ? soFar
            : new ModelUsage(
                part.UsageMetadata.PromptTokenCount ?? soFar.InputTokens,
                part.UsageMetadata.CandidatesTokenCount ?? soFar.OutputTokens,
                Pictures: 0);

    /// <summary>
    /// The material, as the parts of the one user turn; the instruction is the config's own field
    /// and is never concatenated with it.
    /// </summary>
    private static List<Content> Material(Composition request)
    {
        List<Part> parts = [];

        if (request.Material is { Length: > 0 } material)
        {
            parts.Add(new Part { Text = material });
        }

        foreach (var picture in request.Pictures)
        {
            parts.Add(new Part
            {
                InlineData = new Blob { Data = picture.Content.ToArray(), MimeType = picture.MediaType }
            });
        }

        return [new Content { Role = "user", Parts = parts }];
    }

    private Result<Composed> Read(GenerateContentResponse answered)
    {
        if (answered.Text is not { Length: > 0 } json)
        {
            AssistanceLogs.EmptyAnswer(logger, Kind.Code, Reason(answered));

            return AssistanceErrors.UnusableAnswer;
        }

        try
        {
            var answer = JsonSerializer.Deserialize<RecipeAnswer>(json, AssistantHttp.Json);

            return answer is null
                ? AssistanceErrors.UnusableAnswer
                : new Composed(answer.ToDraft(), Usage(answered, pictures: 0));
        }
        catch (JsonException)
        {
            // The model answered in a shape it was not given: ordinary, and the person can retry.
            return AssistanceErrors.UnusableAnswer;
        }
    }

    private Result<Drawn> ReadPicture(GenerateContentResponse answered)
    {
        var picture = (answered.Parts ?? [])
            .Select(part => part.InlineData)
            .FirstOrDefault(blob => blob?.Data is { Length: > 0 });

        if (picture?.Data is not { Length: > 0 } bytes)
        {
            AssistanceLogs.EmptyAnswer(logger, Kind.Code, Reason(answered));

            return AssistanceErrors.UnusableAnswer;
        }

        return new Drawn(new MemoryStream(bytes), Usage(answered, pictures: 1));
    }

    /// <summary>
    /// What it consumed; thinking tokens are left out, so a budget on a reasoning model runs
    /// slightly under the invoice.
    /// </summary>
    private static ModelUsage Usage(GenerateContentResponse answered, int pictures) => new(
        answered.UsageMetadata?.PromptTokenCount ?? 0,
        answered.UsageMetadata?.CandidatesTokenCount ?? 0,
        pictures);

    private static string? Reason(GenerateContentResponse answered) =>
        answered.Candidates?.FirstOrDefault()?.FinishReason?.ToString();

    /// <summary>The id to send, without the <c>models/</c> the listing prefixes.</summary>
    private static string Named(Model model) =>
        (model.Name ?? string.Empty).StartsWith("models/", StringComparison.Ordinal)
            ? model.Name![7..]
            : model.Name ?? string.Empty;

    private static string Labelled(Model model, string id) =>
        string.IsNullOrWhiteSpace(model.DisplayName) ? id : model.DisplayName!;

    /// <summary>
    /// What is left after the models that cannot write or draw a recipe (embedding, retrieval,
    /// answering), by name since the SDK's model type says nothing of what a model does.
    /// </summary>
    private static bool Usable(string id) =>
        !id.Contains("embedding", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("aqa", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("retrieval", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The client for one connection, built per call because the key and address belong to the
    /// connection.
    /// </summary>
    /// <remarks>
    /// The transport is the app's own, so the SDK keeps the hand-written client's rules: no
    /// redirects with a key attached, one pool, one deadline.
    /// </remarks>
    private Client Client(Connected @using, bool drawing = false) => new(
        apiKey: @using.ApiKey,
        httpOptions: new HttpOptions { BaseUrl = @using.BaseUrl },
        clientOptions: new ClientOptions
        {
            HttpClientFactory = () => http.ClientFor(@using.BaseUrl, drawing)
        });

    /// <summary>Classifies a failure and records what the provider said.</summary>
    private Error Failed(Exception failure)
    {
        var error = Failure(failure);

        AssistanceLogs.CallFailed(logger, Kind.Code, error.Code, failure);

        return error;
    }

    /// <summary>The failures that are the provider's rather than this app's.</summary>
    private static bool Expected(Exception failure) =>
        failure is HttpRequestException or OperationCanceledException or IOException
            or InvalidOperationException or JsonException;

    /// <summary>
    /// What a refusal means: a refused credential stays itself here, and becomes "unavailable" only
    /// before reaching somebody cooking.
    /// </summary>
    private static Error Failure(Exception failure) =>
        failure is HttpRequestException { StatusCode: { } status }
            ? status switch
            {
                System.Net.HttpStatusCode.TooManyRequests => AssistanceErrors.Throttled,
                System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                    AssistanceErrors.Rejected,
                System.Net.HttpStatusCode.BadRequest => AssistanceErrors.Refused,
                System.Net.HttpStatusCode.NotFound => AssistanceErrors.ModelMissing,
                _ => AssistanceErrors.Unavailable
            }
            : AssistanceErrors.Unavailable;
}
