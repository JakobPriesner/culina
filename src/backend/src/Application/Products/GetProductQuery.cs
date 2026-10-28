using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Nutrition;
using Application.Telemetry;
using Contracts.Products;
using Domain.Products;
using Domain.Shared;

namespace Application.Products;

/// <summary>Reads what a household, or the nearest one it inherits from, says a barcode is.</summary>
/// <param name="HouseholdId">Which household is asking.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="Code">The barcode as scanned or typed; normalised here.</param>
public sealed record GetProductQuery(Guid HouseholdId, Guid UserId, string Code);

internal sealed class GetProductQueryHandler(
    IHouseholdRepository households,
    IProductRepository products,
    IHouseholdFoodRepository ownFoods)
    : IQueryHandler<GetProductQuery, ProductDetail>
{
    public async Task<Result<ProductDetail>> Handle(GetProductQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Products.Get");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed
            .Bind(() => Gtin.Create(query.Code))
            .Match(
                gtin => FindAsync(query.HouseholdId, gtin, cancellationToken),
                error => Task.FromResult(Result<ProductDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<ProductDetail>> FindAsync(Guid householdId, Gtin gtin, CancellationToken cancellationToken)
    {
        var library = await HouseholdAccess.LibraryAsync(households, householdId, cancellationToken).ConfigureAwait(false);
        var found = await products.FindAsync(library, gtin, cancellationToken).ConfigureAwait(false);

        if (found is null)
        {
            return ProductErrors.Unknown;
        }

        // What the household kept from the pack's label, so a scan in the diary logs it in one step.
        var food = await ownFoods.FindByGtinAsync(library, gtin.Value, cancellationToken).ConfigureAwait(false);

        return found.ToContract() with { Food = food?.ToContract(householdId) };
    }
}
