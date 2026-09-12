namespace Contracts.Households;

/// <summary>One person in a household.</summary>
public sealed record Member
{
    /// <summary>Who.</summary>
    public required Guid UserId { get; init; }

    /// <summary>What they are called.</summary>
    public required string DisplayName { get; init; }

    /// <summary><c>owner</c> or <c>member</c>.</summary>
    public required string Role { get; init; }

    /// <summary>When they joined.</summary>
    public required DateTimeOffset JoinedAt { get; init; }
}
