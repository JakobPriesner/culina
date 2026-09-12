namespace Contracts.Households.CreateInvitation;

/// <summary>A newly issued invitation.</summary>
public sealed record Response
{
    /// <summary>The invitation's id, for revoking it.</summary>
    public required Guid InvitationId { get; init; }

    /// <summary>
    /// The code to share. Shown exactly once — only its digest is stored, so
    /// this value cannot be recovered afterwards.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>When it stops working.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
