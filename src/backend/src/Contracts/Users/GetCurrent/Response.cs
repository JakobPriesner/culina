namespace Contracts.Users.GetCurrent;

/// <summary>The signed-in user, and where they can cook.</summary>
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

    /// <summary>
    /// What the assistant may be asked for on this instance.
    /// </summary>
    public required AssistanceAvailability Assistance { get; init; }

    /// <summary>The entity version, for If-Match on an update.</summary>
    public required long Version { get; init; }
}

/// <summary>
/// Which assistant capabilities are switched on.
/// </summary>
/// <remarks>All false on an unconfigured instance, so the client renders no assistant affordance at all.</remarks>
public sealed record AssistanceAvailability
{
    /// <summary>Rewriting a recipe somebody already has.</summary>
    public required bool Improve { get; init; }

    /// <summary>Writing one from an idea.</summary>
    public required bool Draft { get; init; }

    /// <summary>Reading one out of a photograph or a block of text.</summary>
    public required bool Read { get; init; }

    /// <summary>Drawing a picture.</summary>
    public required bool Draw { get; init; }
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

    /// <summary>
    /// Whose recipes it sees besides its own, nearest first: the household it
    /// inherits from, then the one that inherits from, and so on. Empty when
    /// it inherits nothing. Those recipes can be read and cooked here, never
    /// changed.
    /// </summary>
    public required IReadOnlyList<InheritedHousehold> InheritsFrom { get; init; }
}

/// <summary>A household whose recipes another one sees.</summary>
public sealed record InheritedHousehold
{
    /// <summary>Which household.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What it is called, so a recipe can say where it comes from.</summary>
    public required string Name { get; init; }
}
