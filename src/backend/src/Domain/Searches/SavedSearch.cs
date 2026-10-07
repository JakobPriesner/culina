using Domain.Shared;

namespace Domain.Searches;

/// <summary>
/// A household-owned, named copy of the library toolbar's search.
/// Unversioned on purpose: re-saving over it is a deliberate overwrite, not a clash.
/// </summary>
public sealed class SavedSearch
{
    private SavedSearch(
        Guid id,
        Guid householdId,
        SavedSearchName name,
        SearchCriteria criteria,
        Guid createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        HouseholdId = householdId;
        Name = name;
        Criteria = criteria;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>The saved search's id.</summary>
    public Guid Id { get; }

    /// <summary>Which household owns it.</summary>
    public Guid HouseholdId { get; }

    /// <summary>What it is called.</summary>
    public SavedSearchName Name { get; private set; }

    /// <summary>What it asks the library for.</summary>
    public SearchCriteria Criteria { get; private set; }

    /// <summary>Whose search it was.</summary>
    public Guid CreatedBy { get; }

    /// <summary>When it was saved.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When it last changed.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Saves a search.</summary>
    /// <param name="householdId">Whose kitchen it is for.</param>
    /// <param name="name">What to call it.</param>
    /// <param name="criteria">What it asks for.</param>
    /// <param name="createdBy">Whose search it was.</param>
    /// <param name="now">When.</param>
    public static SavedSearch Create(
        Guid householdId,
        SavedSearchName name,
        SearchCriteria criteria,
        Guid createdBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(criteria);

        return new SavedSearch(CulinaId.New(), householdId, name, criteria, createdBy, now, now);
    }

    /// <summary>Rebuilds a saved search from storage.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="householdId">Whose kitchen.</param>
    /// <param name="name">What it is called.</param>
    /// <param name="criteria">What it asks for.</param>
    /// <param name="createdBy">Whose search it was.</param>
    /// <param name="createdAt">When it was saved.</param>
    /// <param name="updatedAt">When it last changed.</param>
    public static SavedSearch Restore(
        Guid id,
        Guid householdId,
        SavedSearchName name,
        SearchCriteria criteria,
        Guid createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(criteria);

        return new SavedSearch(id, householdId, name, criteria, createdBy, createdAt, updatedAt);
    }

    /// <summary>Renames it and rewrites what it asks for, as one gesture.</summary>
    /// <param name="name">The new name.</param>
    /// <param name="criteria">What it should now ask for.</param>
    /// <param name="now">When.</param>
    public void Revise(SavedSearchName name, SearchCriteria criteria, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(criteria);

        Name = name;
        Criteria = criteria;
        UpdatedAt = now;
    }
}
