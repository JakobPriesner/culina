namespace Domain.Households;

/// <summary>What a member may do in a household: two roles, not a permission matrix.</summary>
public enum HouseholdRole
{
    /// <summary>Sees and edits everything the household owns.</summary>
    Member = 0,

    /// <summary>Additionally invites, removes members, renames and deletes.</summary>
    Owner = 1
}
