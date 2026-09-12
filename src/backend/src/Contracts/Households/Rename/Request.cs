namespace Contracts.Households.Rename;

/// <summary>The new name.</summary>
public sealed record Request
{
    /// <summary>What to call the household.</summary>
    public required string Name { get; init; }
}
