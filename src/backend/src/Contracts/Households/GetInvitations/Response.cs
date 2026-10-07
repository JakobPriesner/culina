namespace Contracts.Households.GetInvitations;

/// <summary>The household's unused invitations.</summary>
public sealed record Response
{
    /// <summary>One entry per open invitation, newest first.</summary>
    public required IReadOnlyList<InvitationSummary> Items { get; init; }
}

/// <summary>An open invitation.</summary>
/// <remarks>
/// Carries no code: it is shown once at creation and only its digest is stored, so listing cannot
/// hand one out.
/// </remarks>
public sealed record InvitationSummary
{
    /// <summary>The invitation's id, for revoking it.</summary>
    public required Guid InvitationId { get; init; }

    /// <summary>Who issued it.</summary>
    public required Guid CreatedBy { get; init; }

    /// <summary>When it was issued.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When it stops working.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
