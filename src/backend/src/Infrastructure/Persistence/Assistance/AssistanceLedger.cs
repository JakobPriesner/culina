using System.Diagnostics;
using Application.Abstractions;
using Domain.Assistance;
using Domain.Shared;

namespace Infrastructure.Persistence.Assistance;

/// <summary>Counts what the assistant has been asked for, and what it cost.</summary>
/// <param name="executor">Runs the SQL.</param>
/// <param name="time">The injected clock, for when a call happened.</param>
internal sealed class AssistanceLedger(DbExecutor executor, TimeProvider time) : IAssistanceLedger
{
    /// <summary>
    /// What still counts against the budget.
    /// </summary>
    /// <remarks>
    /// A settled row costs what it cost; a row still in flight costs what it
    /// reserved. Written once here because the instance total, the personal
    /// total and both of the follow-up reads all have to agree about it — two
    /// spellings of this expression is two budgets.
    /// </remarks>
    private const string Spent = "coalesce(sum(coalesce(cost, estimate)), 0)";

    public async Task<Result<Guid>> ReserveAsync(
        Reservation reservation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var id = CulinaId.New();

        // One statement, so two simultaneous requests cannot both read a total
        // that leaves room and then both write. Whoever the database serves
        // second sees the first one's row.
        var written = await executor.ExecuteAsync(
                $"""
                 insert into assistance_usage
                     (id, user_id, household_id, capability, provider, model,
                      estimate, outcome, occurred_at, request_id)
                 select @id, @userId, @householdId, @capability, @provider, @model,
                        @estimate, 'reserved', @occurredAt, @requestId
                 where (@monthlyBudget is null
                        or (select {Spent} from assistance_usage
                            where occurred_at >= @since) + @estimate <= @monthlyBudget)
                   and (@personalBudget is null
                        or (select {Spent} from assistance_usage
                            where occurred_at >= @since and user_id = @userId)
                            + @estimate <= @personalBudget);
                 """,
                new
                {
                    id,
                    userId = reservation.UserId,
                    householdId = reservation.HouseholdId,
                    capability = reservation.Capability.Code,
                    provider = reservation.Provider.Code,
                    model = reservation.Model,
                    estimate = reservation.Estimate,
                    occurredAt = time.GetUtcNow(),
                    requestId = Activity.Current?.TraceId.ToString(),
                    since = reservation.Since,
                    monthlyBudget = reservation.MonthlyBudget,
                    personalBudget = reservation.PersonalBudget
                },
                cancellationToken)
            .ConfigureAwait(false);

        return written is 1
            ? id
            : await WhichBudgetAsync(reservation, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Works out which of the two ceilings refused, for the error message.
    /// </summary>
    /// <remarks>
    /// A second read, and only ever on the path where nothing is going to
    /// happen anyway. Folding it into the insert would make the statement that
    /// guards every assisted request more complicated to save a query that runs
    /// when somebody has already run out of money.
    /// </remarks>
    private async Task<Result<Guid>> WhichBudgetAsync(
        Reservation reservation,
        CancellationToken cancellationToken)
    {
        var mine = await executor.ExecuteScalarAsync<decimal>(
                $"""
                 select {Spent} from assistance_usage
                 where occurred_at >= @since and user_id = @userId;
                 """,
                new { since = reservation.Since, userId = reservation.UserId },
                cancellationToken)
            .ConfigureAwait(false);

        return reservation.PersonalBudget is { } personal
               && mine + reservation.Estimate > personal
            ? AssistanceErrors.PersonalBudgetExhausted
            : AssistanceErrors.BudgetExhausted;
    }

    public Task SettleAsync(Settlement settlement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settlement);

        return executor.ExecuteAsync(
            """
            update assistance_usage
            set input_tokens = @inputTokens,
                output_tokens = @outputTokens,
                pictures = @pictures,
                cost = @cost,
                outcome = @outcome
            where id = @id;
            """,
            new
            {
                id = settlement.ReservationId,
                inputTokens = settlement.Usage.InputTokens,
                outputTokens = settlement.Usage.OutputTokens,
                pictures = settlement.Usage.Pictures,
                cost = settlement.Cost,
                outcome = settlement.Outcome
            },
            cancellationToken);
    }

    public async Task<UsageSummary> SummariseAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        // Three reads rather than one grouped query read three ways: the totals
        // and the two breakdowns group differently, and a single result set
        // that carried all three would have to be unpicked in C#.
        var totals = await executor.QuerySingleOrDefaultAsync<TotalsRow>(
                """
                select coalesce(sum(cost), 0)                            as cost,
                       coalesce(sum(input_tokens), 0)                    as input_tokens,
                       coalesce(sum(output_tokens), 0)                   as output_tokens,
                       coalesce(sum(pictures), 0)                        as pictures,
                       count(*) filter (where cost is null
                                        and outcome <> 'reserved')::int  as unpriced
                from assistance_usage
                where occurred_at >= @since;
                """,
                new { since },
                cancellationToken)
            .ConfigureAwait(false);

        var people = await executor.QueryAsync<PersonRow>(
                """
                select usage.user_id,
                       users.display_name,
                       count(*)::int            as calls,
                       coalesce(sum(cost), 0)   as cost
                from assistance_usage usage
                join users on users.id = usage.user_id
                where usage.occurred_at >= @since
                group by usage.user_id, users.display_name
                order by cost desc, users.display_name;
                """,
                new { since },
                cancellationToken)
            .ConfigureAwait(false);

        var capabilities = await executor.QueryAsync<CapabilityRow>(
                """
                select capability,
                       count(*)::int            as calls,
                       coalesce(sum(cost), 0)   as cost
                from assistance_usage
                where occurred_at >= @since
                group by capability
                order by cost desc, capability;
                """,
                new { since },
                cancellationToken)
            .ConfigureAwait(false);

        return new UsageSummary(
            totals?.Cost ?? 0m,
            totals?.InputTokens ?? 0,
            totals?.OutputTokens ?? 0,
            totals?.Pictures ?? 0,
            totals?.Unpriced ?? 0,
            [.. people.Select(row =>
                new PersonUsage(row.UserId, row.DisplayName, row.Calls, row.Cost))],
            [.. capabilities.Select(row =>
                new CapabilityUsage(row.Capability, row.Calls, row.Cost))]);
    }

    private sealed record TotalsRow
    {
        public decimal Cost { get; init; }

        public long InputTokens { get; init; }

        public long OutputTokens { get; init; }

        public long Pictures { get; init; }

        public int Unpriced { get; init; }
    }

    private sealed record PersonRow
    {
        public Guid UserId { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public int Calls { get; init; }

        public decimal Cost { get; init; }
    }

    private sealed record CapabilityRow
    {
        public string Capability { get; init; } = string.Empty;

        public int Calls { get; init; }

        public decimal Cost { get; init; }
    }
}
