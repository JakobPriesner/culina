using Contracts.Households;

namespace Contracts.Households.ChangeMemberRole;

/// <summary>The member after the change.</summary>
public sealed record Response
{
    /// <summary>The member's new state.</summary>
    public required Member Member { get; init; }

    /// <summary>The household's new version.</summary>
    public required long Version { get; init; }
}
