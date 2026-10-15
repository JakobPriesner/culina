using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Contracts.Products;
using Domain.Products;
using Domain.Shared;

namespace Application.Products;

/// <summary>Says what a barcode is in a household, naming it the first time or changing what it said.</summary>
/// <param name="HouseholdId">Whose memory; never the household it inherits from.</param>
/// <param name="UserId">Who is saying it.</param>
/// <param name="Code">The barcode as scanned or typed; normalised here.</param>
/// <param name="Details">What the household says.</param>
/// <param name="ExpectedVersion">The version being changed, or null to name a barcode the household has not named.</param>
public sealed record SetProductCommand(
    Guid HouseholdId,
    Guid UserId,
    string Code,
    SetProductRequest Details,
    long? ExpectedVersion);

internal sealed class SetProductCommandHandler(
    IHouseholdRepository households,
    IProductRepository products)
    : ICommandHandler<SetProductCommand, ProductDetail>
{
    public async Task<Result<ProductDetail>> Handle(SetProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Products.Set");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, command.HouseholdId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var details = command.Details;

        var result = await allowed
            .Bind(() => HouseholdProduct.Create(
                command.Code,
                details.Name,
                details.Ingredient,
                details.Quantity,
                details.Unit,
                details.CookMinutes,
                details.Note))
            .Match(
                product => SaveAsync(command, product, cancellationToken),
                error => Task.FromResult(Result<ProductDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<ProductDetail>> SaveAsync(
        SetProductCommand command,
        HouseholdProduct product,
        CancellationToken cancellationToken)
    {
        var saved = command.ExpectedVersion is { } expected
            ? await products.UpdateAsync(command.HouseholdId, product, expected, cancellationToken).ConfigureAwait(false)
            : await products.AddAsync(command.HouseholdId, command.UserId, product, cancellationToken).ConfigureAwait(false);

        return saved.Map(version => new StoredProduct(product, command.HouseholdId, version).ToContract());
    }
}
