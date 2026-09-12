namespace Contracts.Users.GetCurrent;

/// <summary>The signed-in user, and where they can cook.</summary>
/// <remarks>
/// Memberships are included because the app calls this on every boot to resolve
/// the session, and it would otherwise immediately need a second request to
/// know which households exist. One round trip on the critical path is worth a
/// slightly larger response.
/// </remarks>
public sealed record Response
{
    /// <summary>The user's id.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The address they sign in with.</summary>
    public required string Email { get; init; }

    /// <summary>What they are called.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether this account administers the instance.</summary>
    public required bool IsAdmin { get; init; }

    /// <summary>When the account was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The households they belong to.</summary>
    public required IReadOnlyList<HouseholdMembership> Households { get; init; }

    /// <summary>The entity version, for If-Match on an update.</summary>
    public required long Version { get; init; }
}

/// <summary>One household the user belongs to.</summary>
public sealed record HouseholdMembership
{
    /// <summary>The household's id.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What it is called.</summary>
    public required string Name { get; init; }

    /// <summary>What this user may do in it.</summary>
    public required string Role { get; init; }
}
