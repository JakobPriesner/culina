using System.Text.Json;
using Application.Abstractions;
using Domain.Assistance;
using Domain.Shared;
using Microsoft.Extensions.Logging;

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
/// Its native API rather than its OpenAI-compatible one, which would have let
/// this class not exist. Two reasons it is worth the file: the native
/// <c>format</c> field takes a JSON schema directly and is enforced by the
/// runner, where the compatibility layer's <c>response_format</c> has been
/// uneven; and its token counts come back under different names, so an adapter
/// pretending to be the OpenAI one would report zero usage for every call.
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

        var payload = new
        {
            model = @using.Model,
            messages = new object[]
            {
                new { role = "system", content = request.Instruction },
                new
                {
                    role = "user",
                    content = request.Material ?? string.Empty,
                    // Bare base64, not a data URL: this is the one provider of
                    // the three that wants it that way.
                    images = request.Picture.IsEmpty
                        ? null
                        : new[] { Convert.ToBase64String(request.Picture.Span) }
                }
            },
            // Enforced by the runner, so the answer is JSON of this shape or
            // the call fails — which is the same guarantee the hosted two give.
            format = RecipeSchema.Definition,
            stream = false
        };

        var answered = await http.PostAsync<OllamaReply>(
                AssistantHttp.Address(@using.BaseUrl, "api/chat"),
                payload,
                // Nothing to authorise. Said out loud rather than left as an
                // empty lambda nobody can explain.
                _ => { },
                cancellationToken)
            .ConfigureAwait(false);

        return answered.Bind(Read);
    }

    /// <summary>
    /// Refuses, because Ollama serves language and vision models and does not
    /// make pictures.
    /// </summary>
    /// <remarks>
    /// The settings screen does not offer the switch for this provider, so
    /// reaching here means a request arrived for a capability that was turned
    /// on under a different provider and left on. A named refusal rather than a
    /// call that fails somewhere inside the runner.
    /// </remarks>
    public Task<Result<Drawn>> DrawAsync(
        Connected @using,
        Drawing request,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result<Drawn>.Failure(AssistanceErrors.DrawingNotSupported));

    /// <summary>
    /// What has been pulled onto that machine.
    /// </summary>
    /// <remarks>
    /// The one provider where the list is short, honest and entirely the
    /// administrator's doing: it is what they have downloaded. Nothing in it
    /// draws, because Ollama serves language and vision models.
    /// </remarks>
    public async Task<Result<IReadOnlyList<ModelInfo>>> ListModelsAsync(
        Connected @using,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@using);

        var listed = await http.GetAsync<OllamaModelList>(
                AssistantHttp.Address(@using.BaseUrl, "api/tags"),
                _ => { },
                cancellationToken)
            .ConfigureAwait(false);

        return listed.Map(list => (IReadOnlyList<ModelInfo>)
        [
            .. (list.Models ?? [])
                .Select(model => model.Model ?? model.Name ?? string.Empty)
                .Where(id => id.Length > 0)
                .Select(id => new ModelInfo(id, id, CanDraw: false))
                .OrderBy(model => model.Id, StringComparer.Ordinal)
        ]);
    }

    private Result<Composed> Read(OllamaReply reply)
    {
        if (reply.Message?.Content is not { Length: > 0 } json)
        {
            AssistanceLogs.EmptyAnswer(logger, Kind.Code, reply.DoneReason);

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
            // More likely here than with the hosted providers: a small local
            // model asked for a schema will sometimes answer with prose that
            // merely looks like JSON. Ordinary, and asking again usually works.
            return AssistanceErrors.UnusableAnswer;
        }
    }

    private static ModelUsage Usage(OllamaReply reply) =>
        new(reply.PromptEvalCount, reply.EvalCount, Pictures: 0);
}

/// <summary>What has been pulled onto the machine.</summary>
internal sealed record OllamaModelList
{
    public IReadOnlyList<OllamaModel>? Models { get; init; }
}

/// <summary>One pulled model.</summary>
internal sealed record OllamaModel
{
    /// <summary>The tag as it is shown, e.g. <c>llama3.2:latest</c>.</summary>
    public string? Name { get; init; }

    /// <summary>The same thing under the name newer versions use.</summary>
    public string? Model { get; init; }
}

/// <summary>What Ollama's chat endpoint answers with.</summary>
internal sealed record OllamaReply
{
    public OllamaMessage? Message { get; init; }

    public string? DoneReason { get; init; }

    /// <summary>Tokens in the prompt.</summary>
    public int PromptEvalCount { get; init; }

    /// <summary>Tokens generated.</summary>
    public int EvalCount { get; init; }
}

/// <summary>The answer's text.</summary>
internal sealed record OllamaMessage
{
    public string? Content { get; init; }
}
