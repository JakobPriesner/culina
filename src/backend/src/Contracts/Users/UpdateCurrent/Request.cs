namespace Contracts.Users.UpdateCurrent;

/// <summary>What may be changed about your own account.</summary>
public sealed record Request
{
    /// <summary>The new name.</summary>
    public required string DisplayName { get; init; }
}
