using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Contracts.Settings.GetAssistanceUsage;
using Domain.Shared;
using Response = Contracts.Settings.GetAssistanceUsage.Response;

namespace Application.Settings.GetAssistanceUsage;

/// <summary>Reads what the assistant has cost this month.</summary>
public sealed record GetAssistanceUsageQuery;

internal sealed class GetAssistanceUsageQueryHandler(
    IAssistanceLedger ledger,
    AssistanceSettings settings,
    TimeProvider time)
    : IQueryHandler<GetAssistanceUsageQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetAssistanceUsageQuery query,
        CancellationToken cancellationToken)
    {
        using var tracked = UseCaseActivity.Start("Settings.GetAssistanceUsage");

        var since = StartOfMonth(time.GetUtcNow());

        var summary = await ledger.SummariseAsync(since, cancellationToken).ConfigureAwait(false);

        return tracked.Record(Result<Response>.Success(new Response
        {
            Since = since,
            TotalCost = summary.TotalCost,
            MonthlyBudget = settings.MonthlyBudget,
            TotalInputTokens = summary.TotalInputTokens,
            TotalOutputTokens = summary.TotalOutputTokens,
            TotalPictures = summary.TotalPictures,
            Unpriced = summary.Unpriced,
            ByPerson = [.. summary.ByPerson.Select(person => new PersonUsageContract
            {
                UserId = person.UserId,
                DisplayName = person.DisplayName,
                Calls = person.Calls,
                Cost = person.Cost
            })],
            ByCapability = [.. summary.ByCapability.Select(capability => new CapabilityUsageContract
            {
                Capability = capability.Capability,
                Calls = capability.Calls,
                Cost = capability.Cost
            })]
        }));
    }

    /// <summary>
    /// The first instant of the calendar month, in UTC.
    /// </summary>
    /// <remarks>
    /// UTC rather than the instance's local time, and a calendar month rather
    /// than a rolling thirty days, because this number is being compared
    /// against a provider's invoice and that is how a provider bills. A budget
    /// that reset on a different day from the bill would be a budget that
    /// cannot be reconciled with it.
    /// </remarks>
    private static DateTimeOffset StartOfMonth(DateTimeOffset now) =>
        new(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
}
