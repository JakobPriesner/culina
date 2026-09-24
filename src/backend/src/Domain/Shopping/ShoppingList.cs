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
    /// Puts what a recipe asks for on the list, merging it into a line that is
    /// already there.
    /// </summary>
    /// <param name="name">What to buy.</param>
    /// <param name="source">Which recipe asks for it, and how much.</param>
    /// <param name="section">Where in the shop it is found.</param>
    /// <returns>The line it went onto, whether new or existing.</returns>
    /// <remarks>
    /// A line that has already been ticked off is not merged into: it is in the
    /// trolley, and adding to it would quietly change an amount somebody has
    /// already bought.
    /// </remarks>
    public Result<ShoppingListItem> Add(
        ItemName name,
        ShoppingItemSource source,
        ShoppingSection section)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(source);

        var existing = items.FirstOrDefault(item =>
            !item.IsChecked && ItemMergePolicy.CanMerge(item, name, source.Quantity));

        Version += 1;

        if (existing is not null)
        {
            return existing.Receive(source).Map(() => existing);
        }

        var added = ShoppingListItem.Asked(name, source, section, NextSortOrder(section));

        items.Add(added);

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

        var added = ShoppingListItem.Typed(name, quantity, section, NextSortOrder(section));

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

    /// <summary>Whether this planned meal's shopping is already on the list.</summary>
    /// <param name="planEntryId">The planned meal.</param>
    public bool IsShoppedFor(Guid planEntryId) => items.Exists(item => item.IsFor(planEntryId));

    /// <summary>
    /// Counts a recipe that is already here, added by itself, as the shopping
    /// for a planned meal of it.
    /// </summary>
    /// <param name="recipeId">The planned recipe.</param>
    /// <param name="planEntryId">The planned meal.</param>
    /// <returns>Whether the recipe was here to count.</returns>
    /// <remarks>
    /// Somebody who put the waffles on the list from the recipe and then planned
    /// them for Saturday has shopped for Saturday once. Adding the week again
    /// would double every ingredient, silently, and the list would be wrong in
    /// exactly the way nobody checks until the shop.
    /// </remarks>
    public bool CountFor(Guid recipeId, Guid planEntryId)
    {
        var counted = false;

        foreach (var item in items)
        {
            counted |= item.CountFor(recipeId, planEntryId);
        }

        if (counted)
        {
            Version += 1;
        }

        return counted;
    }

    /// <summary>
    /// Takes back exactly what a planned meal put on the list.
    /// </summary>
    /// <param name="planEntryId">The planned meal.</param>
    /// <returns>How many lines changed.</returns>
    /// <remarks>
    /// What is already in the trolley stays: it has been bought, and a list
    /// that un-bought it would be arguing with the shop. A line somebody else
    /// still wants keeps what they want; only a line nothing wants any more
    /// goes.
    /// </remarks>
    public int Withdraw(Guid planEntryId)
    {
        var changed = items.Where(item => !item.IsChecked && item.IsFor(planEntryId)).ToList();

        foreach (var item in changed)
        {
            if (!item.Withdraw(planEntryId))
            {
                items.Remove(item);
            }
        }

        if (changed.Count > 0)
        {
            Version += 1;
        }

        return changed.Count;
    }

    private Result<ShoppingListItem> Find(Guid itemId)
    {
        var item = items.FirstOrDefault(candidate => candidate.Id == itemId);

        return item is null ? ShoppingErrors.ItemNotFound : item;
    }

    private int NextSortOrder(ShoppingSection section) =>
        items.Where(item => item.Section == section).Select(item => item.SortOrder).DefaultIfEmpty(0).Max() + 1;
}
