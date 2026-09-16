using Domain.Shared;

namespace Domain.Cookbooks;

/// <summary>
/// A named shelf of recipes, owned by a household.
/// </summary>
/// <remarks>
/// <para>
/// This class is the shelf's own metadata and nothing else — it never holds the
/// recipes that are on it. A shopping list is bounded by the week and a meal
/// plan by seven days, so both can be loaded whole; a cookbook has no size at
/// all, and reading two hundred rows to rename one would be work nobody asked
/// for. Membership is written through the repository directly, the way the meal
/// plan's entries are.
/// </para>
/// <para>
/// What it does own is the version, because the count of what is on the shelf
/// and the pictures the shelf shows are both part of what its ETag answers for.
/// </para>
/// </remarks>
public sealed class Cookbook
{
    /// <summary>Longer than a shelf label has any reason to be.</summary>
    public const int MaxDescriptionLength = 500;

    private Cookbook(
        Guid id,
        Guid householdId,
        CookbookName name,
        string? description,
        CookbookKind kind,
        CookbookRules rules,
        Guid createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        Id = id;
        HouseholdId = householdId;
        Name = name;
        Description = description;
        Kind = kind;
        Rules = rules;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    /// <summary>The cookbook's id.</summary>
    public Guid Id { get; }

    /// <summary>Which household owns it.</summary>
    public Guid HouseholdId { get; }

    /// <summary>What it is called.</summary>
    public CookbookName Name { get; private set; }

    /// <summary>What it is for, if whoever made it said.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Whether somebody chose what is on it, or its rules do.
    /// </summary>
    /// <remarks>
    /// Fixed at creation. The two answer "why is this recipe here?" with
    /// different kinds of answer, and a shelf that was both could not answer at
    /// all — nor could it say what "take this off" was supposed to mean.
    /// </remarks>
    public CookbookKind Kind { get; }

    /// <summary>What it asks for, or nothing when somebody chooses instead.</summary>
    public CookbookRules Rules { get; private set; }

    /// <summary>
    /// Who made it.
    /// </summary>
    /// <remarks>
    /// Kept although the cookbook belongs to the household, so "Anna's Sunday
    /// roasts" can say whose idea it was without becoming Anna's private
    /// property.
    /// </remarks>
    public Guid CreatedBy { get; }

    /// <summary>When it was made.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When it, or what is on it, last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Bumped by every write. Drives the ETag.</summary>
    public long Version { get; private set; }

    /// <summary>Starts a cookbook.</summary>
    /// <param name="householdId">Whose shelf it is.</param>
    /// <param name="name">What to call it.</param>
    /// <param name="description">What it is for, or null.</param>
    /// <param name="createdBy">Whose idea it was.</param>
    /// <param name="now">When.</param>
    public static Result<Cookbook> Create(
        Guid householdId,
        CookbookName name,
        string? description,
        Guid createdBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (TooLong(description))
        {
            return CookbookErrors.InvalidDescription;
        }

        return new Cookbook(
            CulinaId.New(),
            householdId,
            name,
            Normalise(description),
            CookbookKind.Manual,
            CookbookRules.None,
            createdBy,
            now,
            now,
            version: 1);
    }

    /// <summary>Starts a cookbook that fills itself.</summary>
    /// <param name="householdId">Whose shelf it is.</param>
    /// <param name="name">What to call it.</param>
    /// <param name="description">What it is for, or null.</param>
    /// <param name="rules">What it asks for. At least one.</param>
    /// <param name="createdBy">Whose idea it was.</param>
    /// <param name="now">When.</param>
    public static Result<Cookbook> CreateSmart(
        Guid householdId,
        CookbookName name,
        string? description,
        CookbookRules rules,
        Guid createdBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(rules);

        if (TooLong(description))
        {
            return CookbookErrors.InvalidDescription;
        }

        // A shelf asking for nothing is every recipe you have, which is the
        // screen this one is reached from.
        if (rules.Empty)
        {
            return CookbookErrors.RulesRequired;
        }

        return new Cookbook(
            CulinaId.New(),
            householdId,
            name,
            Normalise(description),
            CookbookKind.Smart,
            rules,
            createdBy,
            now,
            now,
            version: 1);
    }

    /// <summary>Rebuilds a cookbook from storage.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="householdId">Whose shelf.</param>
    /// <param name="name">What it is called.</param>
    /// <param name="description">What it is for.</param>
    /// <param name="kind">Whether somebody chooses what is on it, or its rules do.</param>
    /// <param name="rules">What it asks for, or none.</param>
    /// <param name="createdBy">Whose idea it was.</param>
    /// <param name="createdAt">When it was made.</param>
    /// <param name="updatedAt">When it last changed.</param>
    /// <param name="version">The stored version.</param>
    public static Cookbook Restore(
        Guid id,
        Guid householdId,
        CookbookName name,
        string? description,
        CookbookKind kind,
        CookbookRules rules,
        Guid createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        long version)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(rules);

        return new Cookbook(
            id, householdId, name, description, kind, rules, createdBy, createdAt, updatedAt, version);
    }

    /// <summary>Renames it, and rewrites what it is for.</summary>
    /// <param name="name">The new name.</param>
    /// <param name="description">The new description, or null to clear it.</param>
    /// <param name="rules">
    /// What it should now ask for. Required for a smart cookbook and refused
    /// for a manual one — one method, because the name, the description and the
    /// rules are edited in one form, and an invariant checked in one place is
    /// an invariant that holds.
    /// </param>
    /// <param name="now">When.</param>
    public Result Revise(
        CookbookName name,
        string? description,
        CookbookRules? rules,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (TooLong(description))
        {
            return CookbookErrors.InvalidDescription;
        }

        if (Kind == CookbookKind.Smart && (rules is null || rules.Empty))
        {
            return CookbookErrors.RulesRequired;
        }

        if (Kind == CookbookKind.Manual && rules is { Empty: false })
        {
            return CookbookErrors.RulesDecideMembership;
        }

        Name = name;
        Description = Normalise(description);
        UpdatedAt = now;

        if (Kind == CookbookKind.Smart && rules is not null)
        {
            Rules = rules;
        }

        return Result.Success();
    }

    /// <summary>Takes the version the database assigned.</summary>
    /// <param name="version">What the write returned.</param>
    public void AcceptVersion(long version) => Version = version;

    /// <summary>Notes that what is on the shelf changed.</summary>
    /// <param name="now">When.</param>
    /// <remarks>
    /// Adding or removing a recipe changes the cookbook as anyone reading it
    /// sees it — the count, and the pictures on its cover — so it is a change
    /// to the cookbook, not only to the join table.
    /// </remarks>
    public void Touch(DateTimeOffset now) => UpdatedAt = now;

    private static bool TooLong(string? description) =>
        description?.Trim() is { Length: > MaxDescriptionLength };

    /// <summary>An empty description and no description are the same thing.</summary>
    private static string? Normalise(string? description)
    {
        var trimmed = description?.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
