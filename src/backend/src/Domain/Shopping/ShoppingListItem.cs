using Domain.Ingredients;
using Domain.Planning;
using Domain.Recipes;
using Domain.Shared;

namespace Domain.Shopping;

/// <summary>One line on the list.</summary>
public sealed class ShoppingListItem
{
    private const decimal NegligibleRemainder = 0.01m;

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

    /// <summary>What to buy, in the number its amount asks for: "1 Zwiebel", "3 Zwiebeln".</summary>
    public ItemName Name { get; private set; }

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
    public int SortOrder { get; internal set; }

    /// <summary>Whether a person typed it, or typed some of it, rather than only recipes contributing it.</summary>
    public bool IsManual { get; private set; }

    /// <summary>Which recipes asked for it, and how much each.</summary>
    public IReadOnlyList<ShoppingItemSource> Sources => sources;

    /// <summary>Adds a line a person typed.</summary>
    public static ShoppingListItem Typed(
        ItemName name,
        Quantity quantity,
        ShoppingSection section,
        int sortOrder) =>
        new ShoppingListItem(CulinaId.New(), name, quantity, section, false, null, sortOrder, isManual: true, [])
            .WithFittingName();

    /// <summary>Adds a line a recipe asked for.</summary>
    public static ShoppingListItem Asked(
        ItemName name,
        ShoppingItemSource source,
        ShoppingSection section,
        int sortOrder)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new ShoppingListItem(CulinaId.New(), name, source.Quantity, section, false, null, sortOrder, isManual: false, [source])
            .WithFittingName();
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

        return AddAmount(source.Quantity).Bind(() =>
        {
            sources.Add(source);
            FitName();

            return Result.Success();
        });
    }

    /// <summary>Adds an amount a person typed of the same thing.</summary>
    /// <remarks>
    /// Only called after <see cref="ItemMergePolicy"/> says the two combine. The line counts as typed
    /// from now on, so withdrawing a planned meal keeps what the person added.
    /// </remarks>
    public Result ReceiveTyped(Quantity quantity)
    {
        ArgumentNullException.ThrowIfNull(quantity);

        return AddAmount(quantity).Bind(() =>
        {
            IsManual = true;
            FitName();

            return Result.Success();
        });
    }

    /// <summary>
    /// What is on the line beyond what its sources asked for (typed by hand, or left behind when a recipe changed),
    /// in the line's unit; null when the sources make up all of it, or the line has no amount.
    /// </summary>
    public Quantity? Unattributed()
    {
        var left = Quantity.IsMeasured ? Quantity : null;

        foreach (var source in sources)
        {
            if (left is null)
            {
                return null;
            }

            left = left.Without(source.Quantity);
        }

        // Storing rounds to three places, so a sum of sources can miss its line by a hair.
        return left is { Amount: >= NegligibleRemainder } ? left : null;
    }

    /// <summary>
    /// Whether <paramref name="other"/> is the same thing to buy that this line could not take in: its amount is in a unit that
    /// does not add to this one, or one of them has no amount.
    /// </summary>
    public bool IsSeparateFrom(ShoppingListItem other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return other != this
            && ItemMergePolicy.NamesMatch(Name, other.Name)
            && !ItemMergePolicy.CanMerge(this, other.Name, other.Quantity);
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

    /// <summary>Whether a recipe added by itself (no planned meal) asked for any of it.</summary>
    public bool IsForRecipeAlone(Guid recipeId) =>
        sources.Exists(source => source.RecipeId == recipeId && source.PlanEntryId is null);

    /// <summary>
    /// Takes back exactly what a planned meal asked for; returns whether anything is still left to
    /// buy (another recipe, typed, or unused amount).
    /// </summary>
    public bool Withdraw(Guid planEntryId) => Withdraw(source => source.PlanEntryId == planEntryId);

    /// <summary>Takes back exactly what a recipe added by itself asked for; as <see cref="Withdraw(Guid)"/>.</summary>
    public bool WithdrawRecipeAlone(Guid recipeId) =>
        Withdraw(source => source.RecipeId == recipeId && source.PlanEntryId is null);

    private bool Withdraw(Func<ShoppingItemSource, bool> asked)
    {
        var usedUp = false;

        foreach (var source in sources.Where(asked).ToList())
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
                FitName();
            }
        }

        return !usedUp && (IsManual || sources.Count > 0);
    }

    /// <summary>Changes the amount outright, when a person edits the line.</summary>
    public void SetQuantity(Quantity quantity)
    {
        Quantity = quantity;
        FitName();
    }

    private ShoppingListItem WithFittingName()
    {
        FitName();

        return this;
    }

    // "1 Tomate" and "3 Tomaten" are four Tomaten; a name the table does not list stays as written.
    private void FitName() => FitName([]);

    /// <summary>Puts the name in the number its amount asks for, with the household's own nouns as well.</summary>
    /// <returns>Whether the name changed.</returns>
    internal bool FitName(IReadOnlyList<IngredientNouns.Noun> own)
    {
        var before = Name;

        Name = ItemName.Create(IngredientNouns.ForAmount(Name.Value, Quantity.Amount, Quantity.Unit is not null, language: null, own))
            .Match(fitted => fitted, _ => Name);

        return Name != before;
    }

    // An unmeasured amount ("salt") adds nothing, but still merges.
    private Result AddAmount(Quantity quantity)
    {
        if (!quantity.IsMeasured)
        {
            return Result.Success();
        }

        return Quantity.Add(quantity).Bind(combined =>
        {
            Quantity = combined;

            return Result.Success();
        });
    }
}
