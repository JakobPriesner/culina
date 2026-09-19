using Domain.Assistance;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>
/// What the assistant has been asked for, what it cost, and whether there is
/// any budget left to ask again.
/// </summary>
/// <remarks>
/// <para>
/// The budget check lives here rather than in a decorator over
/// <see cref="IAssistant"/>, and that is deliberate: a cost check hidden inside
/// something whose name says nothing about money is a cost check nobody
/// remembers exists. Both handlers call <see cref="ReserveAsync"/> first, so
/// every one of the four capabilities passes the same gate.
/// </para>
/// <para>
/// Reserve-then-record rather than record-afterwards, because a check that
/// reads the total and then writes a row is a check two simultaneous requests
/// both pass. The reservation is one statement, so the database is what decides
/// who was first.
/// </para>
/// </remarks>
public interface IAssistanceLedger
{
    /// <summary>
    /// Takes a place in this month's budget, or says there is none left.
    /// </summary>
    /// <param name="reservation">Who is asking, for what, and roughly what it costs.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The row to settle afterwards, or why the call may not be made.</returns>
    Task<Result<Guid>> ReserveAsync(Reservation reservation, CancellationToken cancellationToken);

    /// <summary>
    /// Settles a reservation with what actually happened.
    /// </summary>
    /// <param name="settlement">The real token counts, cost and outcome.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <remarks>
    /// Always called, including when the provider failed — a reservation that
    /// was never settled would hold budget nobody spent until the month turned.
    /// </remarks>
    Task SettleAsync(Settlement settlement, CancellationToken cancellationToken);

    /// <summary>What has been spent this period, by whom and on what.</summary>
    /// <param name="since">The start of the period being asked about.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<UsageSummary> SummariseAsync(DateTimeOffset since, CancellationToken cancellationToken);
}

/// <summary>A place taken in the budget, before the call is made.</summary>
/// <param name="UserId">Who asked.</param>
/// <param name="HouseholdId">Whose kitchen it was for, when it was for one.</param>
/// <param name="Capability">What they asked for.</param>
/// <param name="Provider">Which provider.</param>
/// <param name="Model">Which model.</param>
/// <param name="Estimate">
/// What it might cost, used only to decide whether there is room. A guess by
/// definition — the real number is not known until the answer comes back — so
/// it is deliberately generous rather than accurate.
/// </param>
/// <param name="MonthlyBudget">The instance's cap, or null for no cap.</param>
/// <param name="PersonalBudget">One person's share of it, or null for no share.</param>
/// <param name="Since">When this month started.</param>
public sealed record Reservation(
    Guid UserId,
    Guid? HouseholdId,
    Capability Capability,
    AssistantKind Provider,
    string Model,
    decimal Estimate,
    decimal? MonthlyBudget,
    decimal? PersonalBudget,
    DateTimeOffset Since);

/// <summary>What a call actually consumed.</summary>
/// <param name="ReservationId">The row to fill in.</param>
/// <param name="Usage">What the provider said it used.</param>
/// <param name="Cost">
/// What that came to, or null when this app has no price for the model. A
/// number nobody can check is worse than an empty cell.
/// </param>
/// <param name="Outcome">How it went: <c>ok</c>, or the error code.</param>
public sealed record Settlement(
    Guid ReservationId,
    ModelUsage Usage,
    decimal? Cost,
    string Outcome);

/// <summary>This period's spend, as the settings screen shows it.</summary>
/// <param name="TotalCost">Everything spent, where a price was known.</param>
/// <param name="TotalInputTokens">Everything sent.</param>
/// <param name="TotalOutputTokens">Everything received.</param>
/// <param name="TotalPictures">Everything drawn.</param>
/// <param name="Unpriced">
/// How many calls used a model this app has no price for. Shown, because a
/// total that quietly omits them is a total that is wrong.
/// </param>
/// <param name="ByPerson">Who spent what.</param>
/// <param name="ByCapability">What it went on.</param>
public sealed record UsageSummary(
    decimal TotalCost,
    long TotalInputTokens,
    long TotalOutputTokens,
    long TotalPictures,
    int Unpriced,
    IReadOnlyList<PersonUsage> ByPerson,
    IReadOnlyList<CapabilityUsage> ByCapability);

/// <summary>One person's spend this period.</summary>
/// <param name="UserId">Who.</param>
/// <param name="DisplayName">What to call them on screen.</param>
/// <param name="Calls">How many times they asked.</param>
/// <param name="Cost">What it came to.</param>
public sealed record PersonUsage(Guid UserId, string DisplayName, int Calls, decimal Cost);

/// <summary>One capability's spend this period.</summary>
/// <param name="Capability">Which one.</param>
/// <param name="Calls">How many times it was used.</param>
/// <param name="Cost">What it came to.</param>
public sealed record CapabilityUsage(string Capability, int Calls, decimal Cost);
