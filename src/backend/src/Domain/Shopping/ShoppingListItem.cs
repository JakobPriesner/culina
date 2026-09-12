using Domain.Recipes;
using Domain.Shared;

namespace Domain.Shopping;

/// <summary>One line on the list.</summary>
public sealed class ShoppingListItem
{
    private ShoppingListItem(
        Guid id,
        ItemName name,
        Quantity quantity,
        ShoppingSection section,
        bool isChecked,
        DateTimeOffset? checkedAt,
        int sortOrder,
        bool isManual)
    {
        Id = id;
        Name = name;
        Quantity = quantity;
        Section = section;
        IsChecked = isChecked;
        CheckedAt = checkedAt;
        SortOrder = sortOrder;
        IsManual = isManual;
    }

    /// <summary>The item's id.</summary>
    public Guid Id { get; }

    /// <summary>What to buy.</summary>
    public ItemName Name { get; }

    /// <summary>
    /// How much, stored unrounded.
    /// </summary>
    /// <remarks>
    /// Load-bearing: rounding first and summing second compounds error, and
    /// three recipes each contributing a rounded 135 g produce a number nobody
    /// asked for. Rounding is presentation, and presentation belongs to the
    /// client.
    /// </remarks>
    public Quantity Quantity { get; private set; }

    /// <summary>Where in the shop it is found.</summary>
    public ShoppingSection Section { get; private set; }

    /// <summary>Whether it is already in the trolley.</summary>
    public bool IsChecked { get; private set; }

    /// <summary>When it was ticked off, for the "recently bought" grouping.</summary>
    public DateTimeOffset? CheckedAt { get; private set; }

    /// <summary>Its place in its section.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Whether a person typed it rather than a recipe contributing it.</summary>
    public bool IsManual { get; }

    /// <summary>Adds a line.</summary>
    /// <param name="name">What to buy.</param>
    /// <param name="quantity">How much, if the recipe said.</param>
    /// <param name="section">Where in the shop it is found.</param>
    /// <param name="sortOrder">Its place in its section.</param>
    /// <param name="isManual">Whether a person typed it.</param>
    public static ShoppingListItem Create(
        ItemName name,
        Quantity quantity,
        ShoppingSection section,
        int sortOrder,
        bool isManual) =>
        new(CulinaId.New(), name, quantity, section, false, null, sortOrder, isManual);

    /// <summary>Rebuilds one that was stored.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="name">What to buy.</param>
    /// <param name="quantity">How much.</param>
    /// <param name="section">Where in the shop.</param>
    /// <param name="isChecked">Whether it is in the trolley.</param>
    /// <param name="checkedAt">When it was ticked off.</param>
    /// <param name="sortOrder">Its place in its section.</param>
    /// <param name="isManual">Whether a person typed it.</param>
    public static ShoppingListItem Rehydrate(
        Guid id,
        ItemName name,
        Quantity quantity,
        ShoppingSection section,
        bool isChecked,
        DateTimeOffset? checkedAt,
        int sortOrder,
        bool isManual) =>
        new(id, name, quantity, section, isChecked, checkedAt, sortOrder, isManual);

    /// <summary>Ticks it off, or puts it back.</summary>
    /// <param name="isChecked">Whether it is now in the trolley.</param>
    /// <param name="now">The injected clock's reading.</param>
    public void Check(bool isChecked, DateTimeOffset now)
    {
        IsChecked = isChecked;
        CheckedAt = isChecked ? now : null;
    }

    /// <summary>Corrects where a thing is found, for this household.</summary>
    /// <param name="section">The section it actually belongs to.</param>
    public void MoveTo(ShoppingSection section) => Section = section;

    /// <summary>Adds another amount of the same thing.</summary>
    /// <param name="addition">The amount to add.</param>
    /// <remarks>
    /// Only ever called after <see cref="ItemMergePolicy"/> has said the two can
    /// combine, so the units are known to be compatible.
    /// </remarks>
    public Result Add(Quantity addition) =>
        Quantity.Add(addition).Match(
            combined =>
            {
                Quantity = combined;

                return Result.Success();
            },
            Result.Failure);

    /// <summary>Changes the amount outright, when a person edits the line.</summary>
    /// <param name="quantity">The new amount.</param>
    public void SetQuantity(Quantity quantity) => Quantity = quantity;
}
