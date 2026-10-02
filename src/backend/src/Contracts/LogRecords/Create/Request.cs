namespace Contracts.LogRecords.Create;

/// <summary>What the web app noticed since it last reported.</summary>
public sealed record Request
{
    /// <summary>The build that noticed it, as the app knows its own version.</summary>
    public required string AppVersion { get; init; }

    /// <summary>The browser and device it happened on. Optional, so a page built before it existed still reports.</summary>
    public Client? Client { get; init; }

    /// <summary>One to ten records, oldest first.</summary>
    public required IReadOnlyList<Record> Records { get; init; }
}
