using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Domain.Products;
using Domain.Shared;

namespace Application.Products;

/// <summary>Forgets what a household said a barcode is; the households it inherits from keep theirs.</summary>
/// <param name="HouseholdId">Whose memory.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="Code">The barcode as scanned or typed; normalised here.</param>
/// <param name="ExpectedVersion">The version the caller read.</param>
public sealed record RemoveProductCommand(Guid HouseholdId, Guid UserId, string Code, long ExpectedVersion);

internal sealed class RemoveProductCommandHandler(
    IHouseholdRepository households,
    IProductRepository products)
    : ICommandHandler<RemoveProductCommand>
{
    public async Task<Result> Handle(RemoveProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Products.Remove");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed
            .Bind(() => Gtin.Create(command.Code))
            .Match(
                gtin => products.RemoveAsync(command.HouseholdId, gtin, command.ExpectedVersion, cancellationToken),
                error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
