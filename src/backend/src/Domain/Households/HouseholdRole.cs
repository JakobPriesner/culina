namespace Domain.Households;

/// <summary>What a member may do in a household.</summary>
/// <remarks>
/// Two roles, not a permission matrix. A household is the people you cook
/// with; anything finer would be configuration nobody wants to maintain.
/// </remarks>
public enum HouseholdRole
{
    /// <summary>Sees and edits everything the household owns.</summary>
    Member = 0,

    /// <summary>Additionally invites, removes members, renames and deletes.</summary>
    Owner = 1
}
