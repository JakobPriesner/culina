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

    /// <summary>
    /// What the assistant may be asked for on this instance.
    /// </summary>
    /// <remarks>
    /// Here, on the one request the app already makes at boot, rather than on
    /// an endpoint of its own. Every screen that could offer an assistant
    /// affordance needs this answer, and an extra request per page to learn
    /// that the answer is "none" — which it is on most instances — would be a
    /// round trip spent finding out there is nothing to show.
    /// </remarks>
    public required AssistanceAvailability Assistance { get; init; }

    /// <summary>The entity version, for If-Match on an update.</summary>
    public required long Version { get; init; }
}

/// <summary>
/// Which assistant capabilities are switched on.
/// </summary>
/// <remarks>
/// All false on an instance nobody has configured, which is the point: the
/// client renders no assistant affordance at all rather than a disabled one, so
/// Culina without a model looks exactly like Culina did before there was one.
/// </remarks>
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
}
