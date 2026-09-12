namespace Contracts.Users.UpdateCurrent;

/// <summary>The account after the change.</summary>
public sealed record Response
{
    /// <summary>The user's id.</summary>
    public required Guid UserId { get; init; }

    /// <summary>What they are now called.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The new entity version, so the client needs no follow-up read.</summary>
    public required long Version { get; init; }
}
