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
    /// How much, stored unrounded: rounding first and summing second compounds error, and rounding
    /// is presentation.
    /// </summary>
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
    public static ShoppingListItem Typed(
        ItemName name,
        Quantity quantity,
        ShoppingSection section,
        int sortOrder) =>
        new(CulinaId.New(), name, quantity, section, false, null, sortOrder, isManual: true, []);

    /// <summary>Adds a line a recipe asked for.</summary>
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
    public void Check(bool isChecked, DateTimeOffset now)
    {
        IsChecked = isChecked;
        CheckedAt = isChecked ? now : null;
    }

    /// <summary>Corrects where a thing is found, for this household.</summary>
    public void MoveTo(ShoppingSection section) => Section = section;

    /// <summary>Adds what another recipe asks for of the same thing.</summary>
    /// <remarks>
    /// Only called after <see cref="ItemMergePolicy"/> says the two combine. An unmeasured source
    /// ("salt") adds no amount but still counts as one more recipe that asked.
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
    public bool IsFor(Guid planEntryId) => sources.Exists(source => source.PlanEntryId == planEntryId);

    /// <summary>
    /// Counts what a recipe added by itself as the shopping for a planned meal of that recipe;
    /// returns whether anything was counted.
    /// </summary>
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

    /// <summary>
    /// Takes back exactly what a planned meal asked for; returns whether anything is still left to
    /// buy (another recipe, typed, or unused amount).
    /// </summary>
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
    public void SetQuantity(Quantity quantity) => Quantity = quantity;
}
