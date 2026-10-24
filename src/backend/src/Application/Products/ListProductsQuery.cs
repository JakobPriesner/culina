using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Contracts.Products;
using Domain.Shared;

namespace Application.Products;

/// <summary>Lists every barcode a household, and the households it inherits from, have named.</summary>
/// <param name="HouseholdId">Which household is asking.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record ListProductsQuery(Guid HouseholdId, Guid UserId);

internal sealed class ListProductsQueryHandler(
    IHouseholdRepository households,
    IProductRepository products)
    : IQueryHandler<ListProductsQuery, ProductsResponse>
{
    public async Task<Result<ProductsResponse>> Handle(ListProductsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Products.List");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () =>
            {
                var library = await HouseholdAccess
                    .LibraryAsync(households, query.HouseholdId, cancellationToken)
                    .ConfigureAwait(false);
                var stored = await products.ForLibraryAsync(library, cancellationToken).ConfigureAwait(false);

                return Result<ProductsResponse>.Success(new ProductsResponse
                {
                    Items = [.. stored.Select(one => one.ToContract())]
                });
            },
            error => Task.FromResult(Result<ProductsResponse>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
