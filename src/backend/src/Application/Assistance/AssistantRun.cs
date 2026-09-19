using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Assistance;
using Domain.Shared;

namespace Application.Assistance;

/// <summary>
/// One assisted call: allowed, afforded, made, and counted.
/// </summary>
/// <remarks>
/// <para>
/// Every capability goes through here, which is the point. The four checks
/// before a model is called — is the assistant on, is this capability on, is
/// there a provider this knows, is there any budget left — are the same four
/// every time, and a capability that forgot one of them would be a capability
/// that spends money it was told not to.
/// </para>
/// <para>
/// Not a decorator over <see cref="IAssistant"/>. A cost check hidden inside
/// something whose name says nothing about money is a cost check nobody
/// remembers is there; a handler that writes <c>run.ComposeAsync</c> is a
/// handler whose reader can see what it costs.
/// </para>
/// </remarks>
/// <param name="settings">The live instance settings.</param>
/// <param name="assistants">Picks the adapter for the configured provider.</param>
/// <param name="ledger">Decides whether there is budget, and records what was used.</param>
/// <param name="prices">Works out what a call came to.</param>
/// <param name="time">The injected clock, for which month this is.</param>
public sealed class AssistantRun(
    AssistanceSettings settings,
    IAssistants assistants,
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
            settings.ComposeModel,
            ComposeEstimate,
            async (assistant, token) =>
            {
                var answered = await assistant.ComposeAsync(request, token).ConfigureAwait(false);

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
            settings.DrawModel,
            DrawEstimate,
            async (assistant, token) =>
            {
                var answered = await assistant.DrawAsync(request, token).ConfigureAwait(false);

                return answered.Map(drawn => (drawn, drawn.Usage));
            },
            cancellationToken);
    }

    private async Task<Result<TAnswer>> RunAsync<TAnswer>(
        Asker who,
        Capability capability,
        string model,
        decimal estimate,
        Func<IAssistant, CancellationToken, Task<Result<(TAnswer Answer, ModelUsage Usage)>>> call,
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

        if (settings.Kind is not { } provider)
        {
            return AssistanceErrors.UnknownProvider;
        }

        var chosen = assistants.For(provider);

        return await chosen.Match(
            assistant => AfterReservingAsync(who, capability, provider, model, estimate, assistant, call, cancellationToken),
            error => Task.FromResult(Result<TAnswer>.Failure(error))).ConfigureAwait(false);
    }

    private async Task<Result<TAnswer>> AfterReservingAsync<TAnswer>(
        Asker who,
        Capability capability,
        AssistantKind provider,
        string model,
        decimal estimate,
        IAssistant assistant,
        Func<IAssistant, CancellationToken, Task<Result<(TAnswer Answer, ModelUsage Usage)>>> call,
        CancellationToken cancellationToken)
        where TAnswer : notnull
    {
        var reservation = new Reservation(
            who.UserId,
            who.HouseholdId,
            capability,
            provider,
            model,
            estimate,
            settings.MonthlyBudget,
            settings.PersonalBudget,
            StartOfMonth(time.GetUtcNow()));

        var taken = await ledger.ReserveAsync(reservation, cancellationToken).ConfigureAwait(false);

        return await taken.Match(
            id => CallAsync(id, provider, model, assistant, call, cancellationToken),
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
        AssistantKind provider,
        string model,
        IAssistant assistant,
        Func<IAssistant, CancellationToken, Task<Result<(TAnswer Answer, ModelUsage Usage)>>> call,
        CancellationToken cancellationToken)
        where TAnswer : notnull
    {
        var answered = await call(assistant, cancellationToken).ConfigureAwait(false);

        var usage = answered.Match(ok => ok.Usage, _ => default);
        var outcome = answered.Match(_ => "ok", error => error.Code);

        await ledger.SettleAsync(
                new Settlement(
                    reservationId,
                    usage,
                    prices.Of(provider, model, usage.InputTokens, usage.OutputTokens, usage.Pictures),
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
}

/// <summary>Who is asking, for the ledger.</summary>
/// <param name="UserId">The person.</param>
/// <param name="HouseholdId">Their kitchen, when the request is about one.</param>
public sealed record Asker(Guid UserId, Guid? HouseholdId);
