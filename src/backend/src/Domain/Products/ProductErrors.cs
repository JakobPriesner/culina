using Domain.Shared;

namespace Domain.Products;

/// <summary>Every failure the product memory can return.</summary>
public static class ProductErrors
{
    /// <summary>The code is not a barcode a grocery carries, or its check digit is wrong.</summary>
    public static readonly Error InvalidGtin = new(
        "products.invalid_gtin",
        "That is not a valid barcode. Expected 8, 12, 13 or 14 digits with a correct check digit.",
        ErrorType.Validation);

    /// <summary>Nobody in the household, or in a household it inherits from, has named this code.</summary>
    public static readonly Error Unknown = new(
        "products.unknown",
        "This household has not named that barcode yet.",
        ErrorType.NotFound);

    /// <summary>The household already named the code, so changing it needs the version that was read.</summary>
    public static readonly Error AlreadyNamed = new(
        "products.already_named",
        "This household already named that barcode. Read it and send its ETag as If-Match to change it.",
        ErrorType.PreconditionRequired);

    /// <summary>The instance does not look barcodes up at Open Food Facts: its administrator switched it off.</summary>
    public static readonly Error CatalogueOff = new(
        "products.catalogue_off",
        "This server does not look products up at Open Food Facts.",
        ErrorType.NotFound);

    /// <summary>Open Food Facts does not know the barcode, or knows it without a name.</summary>
    public static readonly Error NotInCatalogue = new(
        "products.not_in_catalogue",
        "Open Food Facts does not know that barcode.",
        ErrorType.NotFound);

    /// <summary>The server is keeping under Open Food Facts' request limit; asking again in a minute works.</summary>
    public static readonly Error CatalogueBusy = new(
        "products.catalogue_busy",
        "Open Food Facts was asked too often just now. Try again in a minute.",
        ErrorType.RateLimited);

    /// <summary>Open Food Facts did not answer, or not in a way that could be read.</summary>
    public static readonly Error CatalogueUnavailable = new(
        "products.catalogue_unavailable",
        "Open Food Facts could not be reached. Try again later.",
        ErrorType.Unavailable);

    /// <summary>A search at Open Food Facts needs words: two letters at least, at most 100.</summary>
    public static readonly FieldError InvalidCatalogueQuery = new(
        "q",
        "products.invalid_query",
        "Search with 2 to 100 characters.");

    /// <summary>A product needs a name, at most <see cref="HouseholdProduct.MaxNameLength"/> characters.</summary>
    public static readonly FieldError InvalidName = new(
        "name",
        "products.invalid_name",
        "A product needs a name of at most 200 characters.");

    /// <summary>The ingredient may be left out, but not be longer than a name.</summary>
    public static readonly FieldError InvalidIngredient = new(
        "ingredient",
        "products.invalid_ingredient",
        "An ingredient name must be at most 200 characters.");

    /// <summary>The quantity is what the pack holds, more than 0 and at most 9999999, and needs a unit.</summary>
    public static readonly FieldError InvalidQuantity = new(
        "quantity",
        "products.invalid_quantity",
        "A quantity must be more than 0 and at most 9999999.");

    /// <summary>A unit written as letters, as in a recipe, and only with a quantity.</summary>
    public static readonly FieldError InvalidUnit = new(
        "unit",
        "products.invalid_unit",
        "A unit is a short word such as g, ml or pack, and goes with a quantity.");

    /// <summary>The cooking time is whole minutes, 1 to 600.</summary>
    public static readonly FieldError InvalidCookMinutes = new(
        "cookMinutes",
        "products.invalid_cook_minutes",
        "A cooking time must be 1 to 600 minutes.");

    /// <summary>The note is a line for people, at most 500 characters.</summary>
    public static readonly FieldError InvalidNote = new(
        "note",
        "products.invalid_note",
        "A note must be at most 500 characters.");
}
