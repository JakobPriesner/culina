using Domain.Ingredients;
using Domain.Planning;
using Domain.Recipes;
using Domain.Shared;

namespace Domain.Shopping;

/// <summary>One household's shopping list, created the first time anyone looks at it.</summary>
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
    public static ShoppingList Create(Guid householdId) =>
        new(CulinaId.New(), householdId, [], version: 1);

    /// <summary>Rebuilds one that was stored.</summary>
    public static ShoppingList Rehydrate(
        Guid id,
        Guid householdId,
        IEnumerable<ShoppingListItem> items,
        long version) =>
        new(id, householdId, [.. items], version);

    /// <summary>Puts what a recipe asks for on the list, merging into an existing line.</summary>
    /// <returns>The line it went onto, whether new or existing.</returns>
    /// <remarks>A ticked-off line is never merged into: it is already bought.</remarks>
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

        var added = ShoppingListItem.Asked(name, source, section, PlaceFor(name, section));

        items.Add(added);

        return added;
    }

    /// <summary>Puts something on the list that a person typed, merging into an existing line.</summary>
    /// <returns>The line it went onto, whether new or existing.</returns>
    /// <remarks>"4 EL Tomatenmark" typed next to "1 EL Tomatenmark" is one line of five.</remarks>
    public Result<ShoppingListItem> AddManual(
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
            Version += 1;

            return existing.ReceiveTyped(quantity).Map(() => existing);
        }

        var added = ShoppingListItem.Typed(name, quantity, section, PlaceFor(name, section));

        items.Add(added);
        Version += 1;

        return added;
    }

    /// <summary>Ticks a line off, or puts it back.</summary>
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
    /// Puts every name in the number its amount asks for, knowing the household's own nouns too: two
    /// "Maultasche" lines are "2 Maultaschen" once the household has said so.
    /// </summary>
    /// <param name="own">The household's own nouns.</param>
    public void FitNames(IReadOnlyList<IngredientNouns.Noun> own)
    {
        ArgumentNullException.ThrowIfNull(own);

        var changed = false;

        foreach (var item in items)
        {
            changed |= item.FitName(own);
        }

        if (changed)
        {
            Version += 1;
        }
    }

    /// <summary>Clears what has already been bought.</summary>
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
    public bool IsShoppedFor(Guid planEntryId) => items.Exists(item => item.IsFor(planEntryId));

    /// <summary>Counts a recipe already here, added by itself, as the shopping for a planned meal of it.</summary>
    /// <returns>Whether the recipe was here to count.</returns>
    /// <remarks>Adding the week again would silently double every ingredient.</remarks>
    public bool CountFor(
        Guid recipeId,
        Guid planEntryId,
        DateOnly plannedDate,
        MealSlot plannedSlot)
    {
        var counted = false;

        foreach (var item in items)
        {
            counted |= item.CountFor(recipeId, planEntryId, plannedDate, plannedSlot);
        }

        if (counted)
        {
            Version += 1;
        }

        return counted;
    }

    /// <summary>Takes back exactly what a planned meal put on the list.</summary>
    /// <returns>How many lines changed.</returns>
    /// <remarks>Ticked lines stay (already bought); lines other meals still want keep their share.</remarks>
    public int Withdraw(Guid planEntryId) =>
        Withdraw(item => item.IsFor(planEntryId), item => item.Withdraw(planEntryId));

    /// <summary>Takes back exactly what a recipe added by itself put on the list, as for a planned meal.</summary>
    /// <returns>How many lines changed.</returns>
    public int WithdrawRecipeAlone(Guid recipeId) =>
        Withdraw(item => item.IsForRecipeAlone(recipeId), item => item.WithdrawRecipeAlone(recipeId));

    /// <summary>Sets a line's amount outright, in the unit it already has; a person correcting what to buy.</summary>
    public Result SetAmount(Guid itemId, decimal amount) =>
        Find(itemId).Bind(item => amount is <= 0 or > 100_000
            ? Result.Failure(ShoppingErrors.InvalidQuantity)
            : Quantity.Create(amount, item.Quantity.Unit).Bind(quantity =>
            {
                item.SetQuantity(quantity);
                Version += 1;

                return Result.Success();
            }));

    private int Withdraw(Func<ShoppingListItem, bool> asked, Func<ShoppingListItem, bool> keepAfter)
    {
        var changed = items.Where(item => !item.IsChecked && asked(item)).ToList();

        foreach (var item in changed)
        {
            if (!keepAfter(item))
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

    /// <summary>
    /// Where a new line goes: after the other kinds of the same thing still to buy, so "weißer Spargel" sits under
    /// "grüner Spargel" rather than wherever it arrived; otherwise last in its section.
    /// </summary>
    private int PlaceFor(ItemName name, ShoppingSection section)
    {
        var reading = name.Reading;
        var kinds = items
            .Where(item => item.Section == section && !item.IsChecked &&
                           item.Name.Reading.HasSameHead(reading))
            .ToList();

        if (kinds.Count == 0)
        {
            return NextSortOrder(section);
        }

        var place = kinds.Max(item => item.SortOrder) + 1;

        foreach (var later in items.Where(item => item.Section == section && item.SortOrder >= place))
        {
            later.SortOrder += 1;
        }

        return place;
    }

    private int NextSortOrder(ShoppingSection section) =>
        items.Where(item => item.Section == section).Select(item => item.SortOrder).DefaultIfEmpty(0).Max() + 1;
}
