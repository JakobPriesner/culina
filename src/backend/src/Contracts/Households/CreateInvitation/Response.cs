namespace Contracts.Households.CreateInvitation;

/// <summary>A newly issued invitation.</summary>
public sealed record Response
{
    /// <summary>The invitation's id, for revoking it.</summary>
    public required Guid InvitationId { get; init; }

    /// <summary>The code to share; shown once, as only its digest is stored.</summary>
    public required string Code { get; init; }

    /// <summary>When it stops working.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
