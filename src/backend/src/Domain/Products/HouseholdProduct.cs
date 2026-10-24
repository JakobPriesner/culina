using Domain.Shared;

namespace Domain.Products;

/// <summary>
/// What a household says a barcode is: a name, which ingredient it stands for, and optionally what the pack holds,
/// how long it cooks and a note. Not what the household has at home.
/// </summary>
public sealed record HouseholdProduct
{
    /// <summary>The longest name or ingredient a household may write.</summary>
    public const int MaxNameLength = 200;

    /// <summary>The longest note.</summary>
    public const int MaxNoteLength = 500;

    /// <summary>The most a pack can hold, as the column keeps it.</summary>
    public const decimal MaxQuantity = 9_999_999m;

    /// <summary>The longest cooking time, in minutes: ten hours.</summary>
    public const int MaxCookMinutes = 600;

    private HouseholdProduct()
    {
    }

    /// <summary>The barcode.</summary>
    public required Gtin Gtin { get; init; }

    /// <summary>What to show: "Barilla Spaghetti n.5".</summary>
    public required string Name { get; init; }

    /// <summary>The ingredient to match recipe lines and list items by: "Spaghetti".</summary>
    public string? Ingredient { get; init; }

    /// <summary>What the pack holds, when known.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>The unit of <see cref="Quantity"/>.</summary>
    public string? Unit { get; init; }

    /// <summary>Minutes the household found it needs.</summary>
    public int? CookMinutes { get; init; }

    /// <summary>A line for people.</summary>
    public string? Note { get; init; }

    /// <summary>Reads what a household said, or the first thing wrong with it.</summary>
    /// <param name="code">The barcode as written.</param>
    /// <param name="name">What it is called.</param>
    /// <param name="ingredient">Which ingredient it is; blank is none.</param>
    /// <param name="quantity">What the pack holds.</param>
    /// <param name="unit">The unit of the quantity.</param>
    /// <param name="cookMinutes">Minutes it cooks.</param>
    /// <param name="note">A line for people; blank is none.</param>
    public static Result<HouseholdProduct> Create(
        string? code,
        string? name,
        string? ingredient,
        decimal? quantity,
        string? unit,
        int? cookMinutes,
        string? note) =>
        Gtin.Create(code).Bind(gtin => Describe(gtin, name, ingredient, quantity, unit, cookMinutes, note));

    private static Result<HouseholdProduct> Describe(
        Gtin gtin,
        string? name,
        string? ingredient,
        decimal? quantity,
        string? unit,
        int? cookMinutes,
        string? note)
    {
        var trimmedName = name?.Trim();
        var trimmedIngredient = Blank(ingredient);
        var trimmedNote = Blank(note);
        var writtenUnit = Blank(unit);

        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > MaxNameLength)
        {
            return ProductErrors.InvalidName;
        }

        if (trimmedIngredient?.Length > MaxNameLength)
        {
            return ProductErrors.InvalidIngredient;
        }

        if (quantity is <= 0m or > MaxQuantity)
        {
            return ProductErrors.InvalidQuantity;
        }

        // A unit means something only beside a quantity, and is written the way a recipe's units are.
        var unitCode = writtenUnit is null || quantity is null
            ? null
            : Recipes.Unit.Create(writtenUnit).Match<string?>(one => one.Code, _ => null);

        if (writtenUnit is not null && unitCode is null)
        {
            return ProductErrors.InvalidUnit;
        }

        if (cookMinutes is < 1 or > MaxCookMinutes)
        {
            return ProductErrors.InvalidCookMinutes;
        }

        if (trimmedNote?.Length > MaxNoteLength)
        {
            return ProductErrors.InvalidNote;
        }

        return new HouseholdProduct
        {
            Gtin = gtin,
            Name = trimmedName,
            Ingredient = trimmedIngredient,
            Quantity = quantity,
            Unit = unitCode,
            CookMinutes = cookMinutes,
            Note = trimmedNote
        };
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
