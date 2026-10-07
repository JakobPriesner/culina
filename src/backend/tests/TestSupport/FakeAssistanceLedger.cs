using Application.Abstractions;
using Domain.Assistance;
using Domain.Shared;

namespace TestSupport;

/// <summary>Counts reservations and settlements without a database.</summary>
/// <remarks>Settlements matter most: a failed call must still be settled, or its estimate holds budget until the month turns.</remarks>
public sealed class FakeAssistanceLedger : IAssistanceLedger
{
    private readonly List<Settlement> settled = [];

    private readonly List<Reservation> reserved = [];

    public IReadOnlyList<Reservation> Reservations => reserved;

    public IReadOnlyList<Settlement> Settled => settled;

    /// <summary>When set, every reservation is refused with this.</summary>
    public Error? RefuseWith { get; set; }

    public Task<Result<Guid>> ReserveAsync(
        Reservation reservation,
        CancellationToken cancellationToken)
    {
        reserved.Add(reservation);

        return Task.FromResult(RefuseWith is { } refusal
            ? Result<Guid>.Failure(refusal)
            : Result<Guid>.Success(Guid.CreateVersion7()));
    }

    public Task SettleAsync(Settlement settlement, CancellationToken cancellationToken)
    {
        settled.Add(settlement);

        return Task.CompletedTask;
    }

    public Task<UsageSummary> SummariseAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken) =>
        Task.FromResult(new UsageSummary(0m, 0, 0, 0, 0, [], []));
}

/// <summary>Prices every call the same.</summary>
/// <param name="each">What every call costs.</param>
public sealed class FakeModelPrices(decimal? each = 0.01m) : IModelPrices
{
    public decimal? Of(
        AssistantKind provider,
        string model,
        int inputTokens,
        int outputTokens,
        int pictures) => each;
}
