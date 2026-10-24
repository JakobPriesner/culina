using Application.Abstractions;
using Contracts.Products;

namespace Application.Products;

/// <summary>Stored products to their wire shape.</summary>
internal static class ProductMappings
{
    internal static ProductDetail ToContract(this StoredProduct stored)
    {
        var product = stored.Product;

        return new ProductDetail
        {
            Gtin = product.Gtin.Value,
            Name = product.Name,
            Ingredient = product.Ingredient,
            Quantity = product.Quantity,
            Unit = product.Unit,
            CookMinutes = product.CookMinutes,
            Note = product.Note,
            HouseholdId = stored.HouseholdId,
            Version = stored.Version
        };
    }
}
