using Domain.Shared;

namespace Domain.Cookbooks;

/// <summary>A named shelf of recipes, owned by a household.</summary>
/// <remarks>
/// Holds only the shelf's metadata, never its recipes (a cookbook has no size bound); membership is
/// written through the repository. It owns the version because the count and cover pictures are part of its ETag.
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

    /// <summary>Whether somebody chose what is on it, or its rules do. Fixed at creation.</summary>
    public CookbookKind Kind { get; }

    /// <summary>What it asks for, or nothing when somebody chooses instead.</summary>
    public CookbookRules Rules { get; private set; }

    /// <summary>Who made it. Kept so a cookbook can credit its author without becoming private to them.</summary>
    public Guid CreatedBy { get; }

    /// <summary>When it was made.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When it, or what is on it, last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Bumped by every write. Drives the ETag.</summary>
    public long Version { get; private set; }

    /// <summary>Starts a cookbook.</summary>
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

        // A shelf asking for nothing would be every recipe.
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
    /// <remarks>Rules are required for a smart cookbook and refused for a manual one, checked in one place.</remarks>
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
    public void AcceptVersion(long version) => Version = version;

    /// <summary>Notes that what is on the shelf changed, since the count and cover are part of the cookbook.</summary>
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
