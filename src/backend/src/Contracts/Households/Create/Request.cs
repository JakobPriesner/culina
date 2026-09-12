namespace Contracts.Households.Create;

/// <summary>A new household.</summary>
public sealed record Request
{
    /// <summary>What to call it.</summary>
    public required string Name { get; init; }
}
