using Domain.Shared;

namespace Domain.Shopping;

/// <summary>Failures from the shopping list.</summary>
public static class ShoppingErrors
{
    /// <summary>An item needs something to call it.</summary>
    public static readonly Error NameRequired = new(
        "shopping.name_required",
        "An item needs a name.",
        ErrorType.Validation);

    /// <summary>The name is longer than anything a shopping list needs.</summary>
    public static readonly Error NameTooLong = new(
        "shopping.name_too_long",
        "That name is too long for a shopping list.",
        ErrorType.Validation);

    /// <summary>No such item on this list.</summary>
    public static readonly Error ItemNotFound = new(
        "shopping.item_not_found",
        "That item is not on the list.",
        ErrorType.NotFound);

    /// <summary>The amount is not a plausible one.</summary>
    public static readonly Error InvalidQuantity = new(
        "shopping.invalid_quantity",
        "That is not an amount we can add up.",
        ErrorType.Validation);
}
