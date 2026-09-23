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

/// <summary>
/// Talks to Google's models.
/// </summary>
/// <remarks>
/// <para>
/// Google's own client library rather than this app's reading of their
/// documentation. The fault it removes is the one with no symptom: the token
/// counts were read from fields nothing sends, so every Gemini call in this
/// instance's ledger is recorded as having cost nothing at all.
/// </para>
/// <para>
/// No <c>Microsoft.Extensions.AI</c> package exists for this SDK, so unlike the
/// other two this adapter speaks it directly. The shape of what it does is the
/// same all the same: a system instruction kept apart from the material, a
/// schema the answer must fit, and failures returned rather than thrown.
/// </para>
/// <para>
/// One call does both jobs. An image model answers <c>generateContent</c> with
/// an inline picture where a text model answers with text, so what differs
/// between writing a recipe and drawing one is which parts are asked for and
/// which part is read back.
/// </para>
/// <para>
/// Stateless with respect to configuration: the key, the address and the model
/// all arrive with the call, so one instance serves however many connections an
/// administrator has set up.
/// </para>
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
            return Failure(failure);
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

        // Advanced by hand rather than with `await foreach`, because a `yield`
        // may not live inside a `try` that catches — and every part of this
        // that talks to the provider has to be inside one.
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
                    yield return Stopped(answer, Failure(thrown));

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
            // Asked for explicitly. An image model given no modalities answers
            // with a description of the picture rather than the picture.
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

            // Name order, and no date: Google's listing does not say when a
            // model appeared, and a date invented here would sort this list
            // convincingly and wrongly.
            IReadOnlyList<ModelInfo> everything = ModelLabels.Distinguish(
                [.. listed.OrderBy(model => model.Id, StringComparer.Ordinal)]);

            return Result<IReadOnlyList<ModelInfo>>.Success(
                ModelCatalogue.Narrow(everything, model => Usable(model.Id)));
        }
        catch (Exception failure) when (Expected(failure))
        {
            return Failure(failure);
        }
    }

    /// <summary>How to ask for a recipe, whether or not it is read as it arrives.</summary>
    /// <param name="request">What to do, and what to do it to.</param>
    private static GenerateContentConfig Writing(Composition request) => new()
    {
        // The instruction is a field of its own rather than a turn in the
        // conversation, which is the strongest separation this provider
        // offers between what the app says and what a stranger pasted.
        SystemInstruction = new Content { Parts = [new Part { Text = request.Instruction }] },
        ResponseMimeType = "application/json",
        ResponseJsonSchema = RecipeSchema.Definition
    };

    /// <summary>
    /// The last part of a stream that ended badly.
    /// </summary>
    /// <remarks>
    /// It still carries the recipe, and deliberately: a provider that cut out
    /// after the ingredients wrote something worth keeping, and the screen can
    /// offer it beside the reason rather than throwing away work that was paid
    /// for.
    /// </remarks>
    private static Composing Stopped(PartialRecipe answer, Error failure) => new()
    {
        Recipe = answer.SoFar(),
        Finished = true,
        Failure = failure
    };

    /// <summary>
    /// The counts, which this provider repeats on every chunk.
    /// </summary>
    /// <remarks>
    /// Kept rather than overwritten by an absence: the last chunk of a Gemini
    /// stream carries the totals, but nothing promises that every chunk does,
    /// and a call recorded as free is the fault this adapter's SDK was adopted
    /// to remove.
    /// </remarks>
    private static ModelUsage Counted(GenerateContentResponse part, ModelUsage soFar) =>
        part.UsageMetadata is null
            ? soFar
            : new ModelUsage(
                part.UsageMetadata.PromptTokenCount ?? soFar.InputTokens,
                part.UsageMetadata.CandidatesTokenCount ?? soFar.OutputTokens,
                Pictures: 0);

    /// <summary>
    /// The material, as the parts of the one user turn.
    /// </summary>
    /// <remarks>
    /// The instruction is not here. It is the config's own field, and the two
    /// are never concatenated.
    /// </remarks>
    private static List<Content> Material(Composition request)
    {
        List<Part> parts = [];

        if (request.Material is { Length: > 0 } material)
        {
            parts.Add(new Part { Text = material });
        }

        if (!request.Picture.IsEmpty)
        {
            parts.Add(new Part
            {
                InlineData = new Blob
                {
                    Data = request.Picture.ToArray(),
                    MimeType = request.PictureMediaType ?? "image/jpeg"
                }
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
            // The model answered with something that is not the shape it was
            // given. Ordinary rather than exceptional, and the person asking
            // gets to try again.
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
    /// What it consumed.
    /// </summary>
    /// <remarks>
    /// Thinking is counted apart, in <c>ThoughtsTokenCount</c>, and left out of
    /// both. Google bills it, so a budget built from these two runs slightly
    /// under the invoice on a reasoning model — the alternative is to add a
    /// number to output that the provider does not put there.
    /// </remarks>
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
    /// What is left after the models that cannot write a recipe or draw one.
    /// </summary>
    /// <remarks>
    /// The listing carries embedding, retrieval and answering models too, and
    /// offering those as a choice for "write me a recipe" would be offering
    /// something that cannot answer. By name, because the client's model type
    /// carries a name, a display name and a description and nothing that says
    /// what it does.
    /// </remarks>
    private static bool Usable(string id) =>
        !id.Contains("embedding", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("aqa", StringComparison.OrdinalIgnoreCase)
        && !id.Contains("retrieval", StringComparison.OrdinalIgnoreCase);

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
    private Client Client(Connected @using, bool drawing = false) => new(
        apiKey: @using.ApiKey,
        httpOptions: new HttpOptions { BaseUrl = @using.BaseUrl },
        clientOptions: new ClientOptions
        {
            HttpClientFactory = () => http.ClientFor(@using.BaseUrl, drawing)
        });

    /// <summary>The failures that are the provider's rather than this app's.</summary>
    private static bool Expected(Exception failure) =>
        failure is HttpRequestException or OperationCanceledException or IOException
            or InvalidOperationException or JsonException;

    /// <summary>
    /// What a refusal means.
    /// </summary>
    /// <remarks>
    /// A refused credential is carried as itself this far. It is turned back
    /// into "unavailable" before it can reach somebody who is cooking, but the
    /// settings screen and the ledger are read by the person holding the key.
    /// </remarks>
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
