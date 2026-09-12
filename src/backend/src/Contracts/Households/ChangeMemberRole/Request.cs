namespace Contracts.Households.ChangeMemberRole;

/// <summary>The role to give a member.</summary>
public sealed record Request
{
    /// <summary><c>owner</c> or <c>member</c>.</summary>
    public required string Role { get; init; }
}
