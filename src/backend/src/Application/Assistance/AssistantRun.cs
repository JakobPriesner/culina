using System.Runtime.CompilerServices;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Assistance;
using Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Application.Assistance;

/// <summary>
/// One assisted call: allowed, resolved, afforded, made, and counted.
/// </summary>
/// <remarks>
/// Every capability goes through here so each runs the same checks and none can spend money it was
/// told not to. Resolving (decrypted key, address, model) lives here too, so adapters know nothing
/// about settings or encryption.
/// </remarks>
public sealed partial class AssistantRun(
    AssistanceSettings settings,
    IAssistants assistants,
    ISecretProtector protector,
    IAssistanceLedger ledger,
    IModelPrices prices,
    TimeProvider time,
    ILogger<AssistantRun> logger)
{
    // Reserved before the real cost is known, so two simultaneous requests cannot both fit into the
    // last of the budget. Deliberately generous: ~8 cents of output at the dearest price plus input.
    private const decimal ComposeEstimate = 0.10m;

    private const decimal DrawEstimate = 0.20m;

    /// <summary>Asks for a recipe, if everything about doing so is in order.</summary>
    public Task<Result<DraftedRecipe>> ComposeAsync(
        Asker who,
        Composition request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RunAsync(
            who,
            request.Capability,
            ComposeEstimate,
            async (assistant, connected, token) =>
            {
                var answered = await assistant
                    .ComposeAsync(connected, request, token)
                    .ConfigureAwait(false);

                return answered.Map(composed => (composed.Recipe, composed.Usage));
            },
            cancellationToken);
    }

    /// <summary>Asks for a recipe and hands it over as it is written.</summary>
    /// <remarks>
    /// All checks run before the stream is returned, so "not configured" and "budget spent" stay
    /// ordinary failures; the reservation is settled when the stream closes, however it closes.
    /// </remarks>
    public async Task<Result<IAsyncEnumerable<Composing>>> ComposeStreamAsync(
        Asker who,
        Composition request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prepared = await PrepareAsync(who, request.Capability, ComposeEstimate, cancellationToken)
            .ConfigureAwait(false);

        return prepared.Map(ready => Streaming(ready, request, cancellationToken));
    }

    /// <summary>Asks for a picture, if everything about doing so is in order.</summary>
    public Task<Result<Drawn>> DrawAsync(
        Asker who,
        Drawing request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RunAsync(
            who,
            Capability.Draw,
            DrawEstimate,
            async (assistant, connected, token) =>
            {
                var answered = await assistant
                    .DrawAsync(connected, request, token)
                    .ConfigureAwait(false);

                return answered.Map(drawn => (drawn, drawn.Usage));
            },
            cancellationToken);
    }

    /// <summary>Checks and affords a picture now, so it can be asked for later.</summary>
    /// <remarks>
    /// Drawing is streamed, so failures like 404 or 429 must be known before the 200 is sent.
    /// </remarks>
    public async Task<Result<ReservedDrawing>> ReserveDrawingAsync(
        Asker who,
        CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(who, Capability.Draw, DrawEstimate, cancellationToken)
            .ConfigureAwait(false);

        return prepared.Map(ready => new ReservedDrawing(this, ready));
    }

    /// <summary>A provider already checked and afforded, still to be asked for a picture.</summary>
    public sealed class ReservedDrawing
    {
        private readonly AssistantRun run;
        private readonly Reserved ready;

        internal ReservedDrawing(AssistantRun run, Reserved ready)
        {
            this.run = run;
            this.ready = ready;
        }

        /// <summary>Asks for the picture, and settles whatever it came to.</summary>
        public async Task<Result<Drawn>> AskAsync(
            Drawing request,
            CancellationToken cancellationToken)
        {
            var answered = await ready.Chosen.Assistant
                .DrawAsync(ready.Chosen.Connected, request, cancellationToken)
                .ConfigureAwait(false);

            await run.SettleAsync(
                    ready,
                    answered.Match(drawn => drawn.Usage, _ => default),
                    answered.Match(_ => "ok", error => error.Code))
                .ConfigureAwait(false);

            return answered.Match(
                Result<Drawn>.Success,
                error => Result<Drawn>.Failure(Leaving(error)));
        }
    }

    private async Task<Result<TAnswer>> RunAsync<TAnswer>(
        Asker who,
        Capability capability,
        decimal estimate,
        Func<IAssistant, Connected, CancellationToken, Task<Result<(TAnswer Answer, ModelUsage Usage)>>> call,
        CancellationToken cancellationToken)
        where TAnswer : notnull
    {
        var prepared = await PrepareAsync(who, capability, estimate, cancellationToken)
            .ConfigureAwait(false);

        return await prepared.Match(
            ready => CallAsync(ready, call, cancellationToken),
            error => Task.FromResult(Result<TAnswer>.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Result<Reserved>> PrepareAsync(
        Asker who,
        Capability capability,
        decimal estimate,
        CancellationToken cancellationToken)
    {
        var prepared = await CheckAndReserveAsync(who, capability, estimate, cancellationToken)
            .ConfigureAwait(false);

        prepared.Match(
            ready => Asking(logger, ready.Chosen.Kind.Code, ready.Chosen.Connected.Model, capability.Code),
            error => NotAsking(logger, capability.Code, error.Code));

        return prepared;
    }

    private async Task<Result<Reserved>> CheckAndReserveAsync(
        Asker who,
        Capability capability,
        decimal estimate,
        CancellationToken cancellationToken)
    {
        if (!settings.Allows(capability))
        {
            // One answer for "no assistant" and "not for this job": the caller gains nothing from the difference.
            return settings.IsConnected ? AssistanceErrors.Disabled : AssistanceErrors.NotConfigured;
        }

        var resolved = Resolve(capability);

        return await resolved.Match(
            chosen => ReserveAsync(who, capability, chosen, estimate, cancellationToken),
            error => Task.FromResult(Result<Reserved>.Failure(error))).ConfigureAwait(false);
    }

    // The key is decrypted here and nowhere else, so it lives only for one call.
    private Result<Chosen> Resolve(Capability capability)
    {
        if (settings.UseFor(capability) is not { } use
            || AssistantKind.Parse(use.Provider) is not { } kind)
        {
            return AssistanceErrors.NotConfigured;
        }

        if (settings.ConnectionFor(kind) is not { IsUsable: true } connection)
        {
            return AssistanceErrors.NotConfigured;
        }

        // Null when the stored key cannot be read (key ring lost): that is "not configured".
        var key = kind.NeedsApiKey ? protector.Unprotect(connection.ProtectedApiKey) : string.Empty;

        if (key is null)
        {
            return AssistanceErrors.NotConfigured;
        }

        return assistants.For(kind).Map(assistant => new Chosen(
            assistant,
            kind,
            new Connected(
                key,
                connection.BaseUrl.Length > 0 ? connection.BaseUrl : assistants.HomeOf(kind),
                use.Model.Length > 0 ? use.Model : assistants.DefaultModelFor(kind, capability))));
    }

    private async Task<Result<Reserved>> ReserveAsync(
        Asker who,
        Capability capability,
        Chosen chosen,
        decimal estimate,
        CancellationToken cancellationToken)
    {
        var reservation = new Reservation(
            who.UserId,
            who.HouseholdId,
            capability,
            chosen.Kind,
            chosen.Connected.Model,
            estimate,
            settings.MonthlyBudget,
            settings.PersonalBudget,
            StartOfMonth(time.GetUtcNow()));

        var taken = await ledger.ReserveAsync(reservation, cancellationToken).ConfigureAwait(false);

        return taken.Map(id => new Reserved(id, capability, chosen, time.GetTimestamp()));
    }

    // Settles even a failed call: an unsettled reservation holds its estimate until the month turns.
    private async Task<Result<TAnswer>> CallAsync<TAnswer>(
        Reserved ready,
        Func<IAssistant, Connected, CancellationToken, Task<Result<(TAnswer Answer, ModelUsage Usage)>>> call,
        CancellationToken cancellationToken)
        where TAnswer : notnull
    {
        var answered = await call(ready.Chosen.Assistant, ready.Chosen.Connected, cancellationToken)
            .ConfigureAwait(false);

        await SettleAsync(
                ready,
                answered.Match(ok => ok.Usage, _ => default),
                answered.Match(_ => "ok", error => error.Code))
            .ConfigureAwait(false);

        return answered.Match(
            ok => Result<TAnswer>.Success(ok.Answer),
            error => Result<TAnswer>.Failure(Leaving(error)));
    }

    // Settles in a finally: a closed page abandons the stream, which is an ordinary ending.
    private async IAsyncEnumerable<Composing> Streaming(
        Reserved ready,
        Composition request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var usage = default(ModelUsage);

        // Not "ok" until the stream says so: an unread stream was paid for and produced nothing.
        var outcome = "abandoned";

        try
        {
            var parts = ready.Chosen.Assistant
                .ComposeStreamAsync(ready.Chosen.Connected, request, cancellationToken)
                .ConfigureAwait(false);

            await foreach (var part in parts)
            {
                if (part.Finished)
                {
                    usage = part.Usage;
                    outcome = part.Failure?.Code ?? "ok";
                }

                yield return part.Failure is null
                    ? part
                    : part with { Failure = Leaving(part.Failure) };
            }
        }
        finally
        {
            await SettleAsync(ready, usage, outcome).ConfigureAwait(false);
        }
    }

    private Task SettleAsync(Reserved ready, ModelUsage usage, string outcome)
    {
        // A failed or abandoned call that reported no usage may still have been billed: its cost is
        // unknown (null), so the reservation's estimate keeps counting rather than a false zero.
        // A provider that needs no key (a local Ollama) bills nothing either way.
        var cost = outcome != "ok" && usage == default && ready.Chosen.Kind.NeedsApiKey
            && !RefusedBeforeGenerating(outcome)
            ? null
            : prices.Of(
                ready.Chosen.Kind,
                ready.Chosen.Connected.Model,
                usage.InputTokens,
                usage.OutputTokens,
                usage.Pictures);
        var elapsed = (long)time.GetElapsedTime(ready.StartedAt).TotalMilliseconds;

        Settled(
            logger,
            ready.Capability.Code,
            ready.Chosen.Kind.Code,
            ready.Chosen.Connected.Model,
            outcome,
            elapsed,
            usage.InputTokens,
            usage.OutputTokens,
            usage.Pictures,
            cost);

        return ledger.SettleAsync(
            new Settlement(ready.ReservationId, usage, cost, outcome),
            // Not the caller's token: a cancelled request must still account for what it spent.
            CancellationToken.None);
    }

    [LoggerMessage(
        EventId = 1503,
        Level = LogLevel.Debug,
        Message = "Asking {Provider} {Model} for {Capability}")]
    private static partial void Asking(ILogger logger, string provider, string model, string capability);

    [LoggerMessage(
        EventId = 1504,
        Level = LogLevel.Debug,
        Message = "Not asking an assistant for {Capability}: {Code}")]
    private static partial void NotAsking(ILogger logger, string capability, string code);

    [LoggerMessage(
        EventId = 1502,
        Level = LogLevel.Information,
        Message = "Assistant call for {Capability} on {Provider} {Model} ended {Outcome} in {ElapsedMilliseconds} ms: "
            + "{InputTokens} tokens in, {OutputTokens} out, {Pictures} pictures, cost {Cost}")]
    private static partial void Settled(
        ILogger logger,
        string capability,
        string provider,
        string model,
        string outcome,
        long elapsedMilliseconds,
        int inputTokens,
        int outputTokens,
        int pictures,
        decimal? cost);

    // Refusals that come back before the model generates anything, so they cost nothing; counting
    // their estimate would let a burst of 429s lock the budget.
    private static bool RefusedBeforeGenerating(string outcome) =>
        outcome == AssistanceErrors.Throttled.Code
        || outcome == AssistanceErrors.Rejected.Code
        || outcome == AssistanceErrors.ModelMissing.Code
        || outcome == AssistanceErrors.Refused.Code;

    // The ledger keeps the precise code; a refused key is the admin's to fix, so users see "unavailable".
    private static Error Leaving(Error error) =>
        error == AssistanceErrors.Rejected ? AssistanceErrors.Unavailable : error;

    /// <summary>The first instant of the calendar month, in UTC, matching how providers bill.</summary>
    internal static DateTimeOffset StartOfMonth(DateTimeOffset now) =>
        new(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>An adapter and the connection it is about to be called with.</summary>
    internal sealed record Chosen(IAssistant Assistant, AssistantKind Kind, Connected Connected);

    /// <summary>A provider to call, the money already set aside for it, and when.</summary>
    internal sealed record Reserved(Guid ReservationId, Capability Capability, Chosen Chosen, long StartedAt);
}

/// <summary>Who is asking, for the ledger.</summary>
public sealed record Asker(Guid UserId, Guid? HouseholdId);
