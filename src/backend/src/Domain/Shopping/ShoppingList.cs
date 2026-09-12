using Domain.Recipes;
using Domain.Shared;

namespace Domain.Shopping;

/// <summary>
/// One household's shopping list.
/// </summary>
/// <remarks>
/// Exactly one per household, created the first time anyone looks at it. One
/// list and not many: a second list is a planning feature, and planning is not
/// what a shopping list is for.
/// </remarks>
public sealed class ShoppingList
{
    private readonly List<ShoppingListItem> items;

    private ShoppingList(Guid id, Guid householdId, List<ShoppingListItem> items, long version)
    {
        Id = id;
        HouseholdId = householdId;
        this.items = items;
        Version = version;
    }

    /// <summary>The list's id.</summary>
    public Guid Id { get; }

    /// <summary>Whose list it is.</summary>
    public Guid HouseholdId { get; }

    /// <summary>What is on it.</summary>
    public IReadOnlyList<ShoppingListItem> Items => items;

    /// <summary>The entity version.</summary>
    public long Version { get; private set; }

    /// <summary>Starts a household's list.</summary>
    /// <param name="householdId">Whose list.</param>
    public static ShoppingList Create(Guid householdId) =>
        new(CulinaId.New(), householdId, [], version: 1);

    /// <summary>Rebuilds one that was stored.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="householdId">Whose list.</param>
    /// <param name="items">What is on it.</param>
    /// <param name="version">The stored version.</param>
    public static ShoppingList Rehydrate(
        Guid id,
        Guid householdId,
        IEnumerable<ShoppingListItem> items,
        long version) =>
        new(id, householdId, [.. items], version);

    /// <summary>
    /// Puts something on the list, merging it into a line that is already there.
    /// </summary>
    /// <param name="name">What to buy.</param>
    /// <param name="quantity">How much.</param>
    /// <param name="section">Where in the shop it is found.</param>
    /// <returns>The line it went onto, whether new or existing.</returns>
    /// <remarks>
    /// A line that has already been ticked off is not merged into: it is in the
    /// trolley, and adding to it would quietly change an amount somebody has
    /// already bought.
    /// </remarks>
    public Result<ShoppingListItem> Add(
        ItemName name,
        Quantity quantity,
        ShoppingSection section)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(quantity);

        var existing = items.FirstOrDefault(item =>
            !item.IsChecked && ItemMergePolicy.CanMerge(item, name, quantity));

        if (existing is not null)
        {
            return quantity.IsMeasured
                ? existing.Add(quantity).Map(() => existing)
                : existing;
        }

        var added = ShoppingListItem.Create(name, quantity, section, NextSortOrder(section), isManual: false);

        items.Add(added);
        Version += 1;

        return added;
    }

    /// <summary>Puts something on the list that a person typed.</summary>
    /// <param name="name">What to buy.</param>
    /// <param name="quantity">How much, if they said.</param>
    /// <param name="section">Where in the shop it is found.</param>
    public Result<ShoppingListItem> AddManual(
        ItemName name,
        Quantity quantity,
        ShoppingSection section)
    {
        ArgumentNullException.ThrowIfNull(name);

        var added = ShoppingListItem.Create(name, quantity, section, NextSortOrder(section), isManual: true);

        items.Add(added);
        Version += 1;

        return added;
    }

    /// <summary>Ticks a line off, or puts it back.</summary>
    /// <param name="itemId">Which line.</param>
    /// <param name="isChecked">Whether it is now in the trolley.</param>
    /// <param name="now">The injected clock's reading.</param>
    public Result Check(Guid itemId, bool isChecked, DateTimeOffset now) =>
        Find(itemId).Match(
            item =>
            {
                item.Check(isChecked, now);
                Version += 1;

                return Result.Success();
            },
            Result.Failure);

    /// <summary>Corrects where a thing is found.</summary>
    /// <param name="itemId">Which line.</param>
    /// <param name="section">The section it actually belongs to.</param>
    public Result MoveToSection(Guid itemId, ShoppingSection section) =>
        Find(itemId).Match(
            item =>
            {
                item.MoveTo(section);
                Version += 1;

                return Result.Success();
            },
            Result.Failure);

    /// <summary>Takes a line off the list.</summary>
    /// <param name="itemId">Which line.</param>
    public Result Remove(Guid itemId) =>
        Find(itemId).Match(
            item =>
            {
                items.Remove(item);
                Version += 1;

                return Result.Success();
            },
            Result.Failure);

    /// <summary>
    /// Clears what has already been bought.
    /// </summary>
    /// <remarks>
    /// The one bulk action worth having: after a shop, everything ticked is
    /// done with, and removing them one at a time is the tedium the list exists
    /// to avoid.
    /// </remarks>
    public int ClearChecked()
    {
        var removed = items.RemoveAll(item => item.IsChecked);

        if (removed > 0)
        {
            Version += 1;
        }

        return removed;
    }

    private Result<ShoppingListItem> Find(Guid itemId)
    {
        var item = items.FirstOrDefault(candidate => candidate.Id == itemId);

        return item is null ? ShoppingErrors.ItemNotFound : item;
    }

    private int NextSortOrder(ShoppingSection section) =>
        items.Where(item => item.Section == section).Select(item => item.SortOrder).DefaultIfEmpty(0).Max() + 1;
}
