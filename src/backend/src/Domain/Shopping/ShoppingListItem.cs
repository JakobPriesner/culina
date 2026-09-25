using Domain.Planning;
using Domain.Recipes;
using Domain.Shared;

namespace Domain.Shopping;

/// <summary>One line on the list.</summary>
public sealed class ShoppingListItem
{
    private readonly List<ShoppingItemSource> sources;

    private ShoppingListItem(
        Guid id,
        ItemName name,
        Quantity quantity,
        ShoppingSection section,
        bool isChecked,
        DateTimeOffset? checkedAt,
        int sortOrder,
        bool isManual,
        List<ShoppingItemSource> sources)
    {
        Id = id;
        Name = name;
        Quantity = quantity;
        Section = section;
        IsChecked = isChecked;
        CheckedAt = checkedAt;
        SortOrder = sortOrder;
        IsManual = isManual;
        this.sources = sources;
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

    /// <summary>Which recipes asked for it, and how much each.</summary>
    public IReadOnlyList<ShoppingItemSource> Sources => sources;

    /// <summary>Adds a line a person typed.</summary>
    /// <param name="name">What to buy.</param>
    /// <param name="quantity">How much, if they said.</param>
    /// <param name="section">Where in the shop it is found.</param>
    /// <param name="sortOrder">Its place in its section.</param>
    public static ShoppingListItem Typed(
        ItemName name,
        Quantity quantity,
        ShoppingSection section,
        int sortOrder) =>
        new(CulinaId.New(), name, quantity, section, false, null, sortOrder, isManual: true, []);

    /// <summary>Adds a line a recipe asked for.</summary>
    /// <param name="name">What to buy.</param>
    /// <param name="source">Which recipe, and how much of it.</param>
    /// <param name="section">Where in the shop it is found.</param>
    /// <param name="sortOrder">Its place in its section.</param>
    public static ShoppingListItem Asked(
        ItemName name,
        ShoppingItemSource source,
        ShoppingSection section,
        int sortOrder)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new(CulinaId.New(), name, source.Quantity, section, false, null, sortOrder, isManual: false, [source]);
    }

    /// <summary>Rebuilds one that was stored.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="name">What to buy.</param>
    /// <param name="quantity">How much.</param>
    /// <param name="section">Where in the shop.</param>
    /// <param name="isChecked">Whether it is in the trolley.</param>
    /// <param name="checkedAt">When it was ticked off.</param>
    /// <param name="sortOrder">Its place in its section.</param>
    /// <param name="isManual">Whether a person typed it.</param>
    /// <param name="sources">Which recipes asked for it.</param>
    public static ShoppingListItem Rehydrate(
        Guid id,
        ItemName name,
        Quantity quantity,
        ShoppingSection section,
        bool isChecked,
        DateTimeOffset? checkedAt,
        int sortOrder,
        bool isManual,
        IEnumerable<ShoppingItemSource> sources) =>
        new(id, name, quantity, section, isChecked, checkedAt, sortOrder, isManual, [.. sources]);

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

    /// <summary>Adds what another recipe asks for of the same thing.</summary>
    /// <param name="source">Which recipe, and how much of it.</param>
    /// <remarks>
    /// Only ever called after <see cref="ItemMergePolicy"/> has said the two can
    /// combine, so the units are known to be compatible. An unmeasured source —
    /// "salt" — adds nothing to the amount, but it is still one more recipe that
    /// asked for it.
    /// </remarks>
    public Result Receive(ShoppingItemSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.Quantity.IsMeasured)
        {
            sources.Add(source);

            return Result.Success();
        }

        return Quantity.Add(source.Quantity).Match(
            combined =>
            {
                Quantity = combined;
                sources.Add(source);

                return Result.Success();
            },
            Result.Failure);
    }

    /// <summary>Whether this planned meal asked for any of it.</summary>
    /// <param name="planEntryId">The planned meal.</param>
    public bool IsFor(Guid planEntryId) => sources.Exists(source => source.PlanEntryId == planEntryId);

    /// <summary>
    /// Counts what a recipe added by itself as the shopping for a planned meal
    /// of that recipe.
    /// </summary>
    /// <param name="recipeId">The recipe.</param>
    /// <param name="planEntryId">The planned meal it is now for.</param>
    /// <param name="plannedDate">The day the meal is planned for.</param>
    /// <param name="plannedSlot">The meal of that day.</param>
    /// <returns>Whether anything was counted.</returns>
    public bool CountFor(
        Guid recipeId,
        Guid planEntryId,
        DateOnly plannedDate,
        MealSlot plannedSlot)
    {
        var counted = false;

        for (var index = 0; index < sources.Count; index++)
        {
            if (sources[index].RecipeId == recipeId && sources[index].PlanEntryId is null)
            {
                sources[index] = sources[index] with
                {
                    PlanEntryId = planEntryId,
                    PlannedDate = plannedDate,
                    PlannedSlot = plannedSlot
                };
                counted = true;
            }
        }

        return counted;
    }

    /// <summary>Takes back exactly what a planned meal asked for.</summary>
    /// <param name="planEntryId">The planned meal.</param>
    /// <returns>
    /// Whether anything is still left to buy: something another recipe asks
    /// for, something a person typed, or an amount the meal did not use up.
    /// </returns>
    public bool Withdraw(Guid planEntryId)
    {
        var usedUp = false;

        foreach (var source in sources.Where(source => source.PlanEntryId == planEntryId).ToList())
        {
            sources.Remove(source);

            if (!source.Quantity.IsMeasured)
            {
                continue;
            }

            var left = Quantity.Without(source.Quantity);

            if (left is null)
            {
                usedUp = true;
            }
            else
            {
                Quantity = left;
            }
        }

        return !usedUp && (IsManual || sources.Count > 0);
    }

    /// <summary>Changes the amount outright, when a person edits the line.</summary>
    /// <param name="quantity">The new amount.</param>
    public void SetQuantity(Quantity quantity) => Quantity = quantity;
}
