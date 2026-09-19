using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Assistance;
using Domain.Shared;

namespace Application.Assistance;

/// <summary>
/// One assisted call: allowed, resolved, afforded, made, and counted.
/// </summary>
/// <remarks>
/// <para>
/// Every capability goes through here, which is the point. The checks before a
/// model is called are the same every time — is the assistant on, is this job
/// switched on, was a provider chosen for it, is that provider connected, can
/// it do this job, and is there any budget left — and a capability that forgot
/// one of them would be a capability that spends money it was told not to.
/// </para>
/// <para>
/// Resolving lives here too, and that is what lets one instance use three
/// providers at once. The settings say <em>which</em> provider does this job;
/// this turns that into a decrypted key, an address and a model name, and hands
/// the adapter something it can simply call. Adapters therefore know nothing
/// about settings, encryption, or defaults.
/// </para>
/// <para>
/// Not a decorator over <see cref="IAssistant"/>. A cost check hidden inside
/// something whose name says nothing about money is a cost check nobody
/// remembers is there; a handler that writes <c>run.ComposeAsync</c> is a
/// handler whose reader can see what it costs.
/// </para>
/// </remarks>
/// <param name="settings">The live instance settings.</param>
/// <param name="assistants">The adapter for each provider.</param>
/// <param name="protector">Decrypts a stored key.</param>
/// <param name="ledger">Decides whether there is budget, and records what was used.</param>
/// <param name="prices">Works out what a call came to.</param>
/// <param name="time">The injected clock, for which month this is.</param>
public sealed class AssistantRun(
    AssistanceSettings settings,
    IAssistants assistants,
    ISecretProtector protector,
    IAssistanceLedger ledger,
    IModelPrices prices,
    TimeProvider time)
{
    /// <summary>
    /// What one composition might cost, reserved before the real number exists.
    /// </summary>
    /// <remarks>
    /// A guess, and deliberately a generous one: its only job is to stop two
    /// simultaneous requests both fitting into the last of the money, and it is
    /// replaced by the real figure a few seconds later. Erring high means a
    /// budget stops slightly early under load, which is the safe direction.
    /// </remarks>
    private const decimal ComposeEstimate = 0.05m;

    /// <summary>What one picture might cost. Dearer, and more variable.</summary>
    private const decimal DrawEstimate = 0.20m;

    /// <summary>Asks for a recipe, if everything about doing so is in order.</summary>
    /// <param name="who">Who is asking, and for which kitchen.</param>
    /// <param name="request">What to do, and what to do it to.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
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

    /// <summary>Asks for a picture, if everything about doing so is in order.</summary>
    /// <param name="who">Who is asking, and for which kitchen.</param>
    /// <param name="request">What to draw.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
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

    private async Task<Result<TAnswer>> RunAsync<TAnswer>(
        Asker who,
        Capability capability,
        decimal estimate,
        Func<IAssistant, Connected, CancellationToken, Task<Result<(TAnswer Answer, ModelUsage Usage)>>> call,
        CancellationToken cancellationToken)
        where TAnswer : notnull
    {
        if (!settings.Allows(capability))
        {
            // One answer for "no assistant here" and "not that, here". A caller
            // learns nothing from being told which, and the affordance that
            // asked should not have been on screen either way.
            return settings.IsConnected ? AssistanceErrors.Disabled : AssistanceErrors.NotConfigured;
        }

        var resolved = Resolve(capability);

        return await resolved.Match(
            chosen => AfterReservingAsync(who, capability, chosen, estimate, call, cancellationToken),
            error => Task.FromResult(Result<TAnswer>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns "this job uses that provider" into something callable.
    /// </summary>
    /// <remarks>
    /// The key is decrypted here and nowhere else, so it exists in memory for
    /// the length of one call and never reaches a class whose job is HTTP. A
    /// key that cannot be decrypted — a key ring that was lost and came back
    /// empty — reads as "not configured", because that is what it is.
    /// </remarks>
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

        // Null only for a provider that needs a key and whose stored one cannot
        // be read — a key ring lost and restored empty. That is "not
        // configured", because it is.
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

    private async Task<Result<TAnswer>> AfterReservingAsync<TAnswer>(
        Asker who,
        Capability capability,
        Chosen chosen,
        decimal estimate,
        Func<IAssistant, Connected, CancellationToken, Task<Result<(TAnswer Answer, ModelUsage Usage)>>> call,
        CancellationToken cancellationToken)
        where TAnswer : notnull
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

        return await taken.Match(
            id => CallAsync(id, chosen, call, cancellationToken),
            error => Task.FromResult(Result<TAnswer>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>
    /// Makes the call, and settles the reservation however it goes.
    /// </summary>
    /// <remarks>
    /// The settlement is not optional and not conditional. A reservation that
    /// was never settled holds its estimate against the budget until the month
    /// turns — so a failed call must still write its row, with whatever tokens
    /// it managed to consume and the error code as its outcome.
    /// </remarks>
    private async Task<Result<TAnswer>> CallAsync<TAnswer>(
        Guid reservationId,
        Chosen chosen,
        Func<IAssistant, Connected, CancellationToken, Task<Result<(TAnswer Answer, ModelUsage Usage)>>> call,
        CancellationToken cancellationToken)
        where TAnswer : notnull
    {
        var answered = await call(chosen.Assistant, chosen.Connected, cancellationToken)
            .ConfigureAwait(false);

        var usage = answered.Match(ok => ok.Usage, _ => default);
        var outcome = answered.Match(_ => "ok", error => error.Code);

        await ledger.SettleAsync(
                new Settlement(
                    reservationId,
                    usage,
                    prices.Of(
                        chosen.Kind,
                        chosen.Connected.Model,
                        usage.InputTokens,
                        usage.OutputTokens,
                        usage.Pictures),
                    outcome),
                // Not the caller's token: a cancelled request must still leave
                // the budget it spent accounted for.
                CancellationToken.None)
            .ConfigureAwait(false);

        return answered.Map(ok => ok.Answer);
    }

    /// <summary>The first instant of the calendar month, in UTC.</summary>
    /// <param name="now">The injected present.</param>
    /// <remarks>
    /// A calendar month because that is how a provider bills, and UTC because a
    /// budget that reset on a different day from the invoice could not be
    /// reconciled with it.
    /// </remarks>
    internal static DateTimeOffset StartOfMonth(DateTimeOffset now) =>
        new(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>An adapter and the connection it is about to be called with.</summary>
    private sealed record Chosen(IAssistant Assistant, AssistantKind Kind, Connected Connected);
}

/// <summary>Who is asking, for the ledger.</summary>
/// <param name="UserId">The person.</param>
/// <param name="HouseholdId">Their kitchen, when the request is about one.</param>
public sealed record Asker(Guid UserId, Guid? HouseholdId);
