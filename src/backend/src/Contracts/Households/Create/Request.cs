namespace Contracts.Households.Create;

/// <summary>A new household.</summary>
public sealed record Request
{
    /// <summary>What to call it.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// A household you are in whose recipes the new one should see from the
    /// start, or null for none.
    /// </summary>
    public Guid? InheritsFrom { get; init; }
}
