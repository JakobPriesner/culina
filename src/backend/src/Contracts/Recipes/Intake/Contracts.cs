namespace Contracts.Recipes.Intake;

/// <summary>A persisted import and its recoverable progress.</summary>
public sealed record IntakeJob
{
    /// <summary>The import identity, also used to deduplicate submissions.</summary>
    public required Guid Id { get; init; }
    /// <summary>The requesting household.</summary>
    public required Guid HouseholdId { get; init; }
    /// <summary>Queued, reading, thinking, writing, saving, ready, failed or reviewed.</summary>
    public required string Stage { get; init; }
    /// <summary>When the import was accepted.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
    /// <summary>The recipe, saved before ready is published.</summary>
    public Guid? RecipeId { get; init; }
    /// <summary>The last complete fields written by the assistant.</summary>
    public Drafts.Response? Draft { get; init; }
    /// <summary>The original written source.</summary>
    public string? Material { get; init; }
    /// <summary>The original spoken source.</summary>
    public string? Transcript { get; init; }
    /// <summary>The original address.</summary>
    public string? SourceUrl { get; init; }
    /// <summary>How many original images can be read.</summary>
    public int PhotoCount { get; init; }
    /// <summary>An ordinary failure code, without provider secrets.</summary>
    public string? ErrorCode { get; init; }
}

/// <summary>One message on the stream of a person's imports.</summary>
public sealed record IntakeEvent
{
    /// <summary>True when the jobs are every import, replacing what the reader has; sent first, and again after a reconnect.</summary>
    public required bool Snapshot { get; init; }
    /// <summary>The imports, or for a later event only the ones that changed (stage reviewed means gone); none is a heartbeat.</summary>
    public required IReadOnlyList<IntakeJob> Jobs { get; init; }
}

/// <summary>The browser's Web Push subscription.</summary>
public sealed record PushRegistration
{
    /// <summary>The push service address.</summary>
    public required string Endpoint { get; init; }
    /// <summary>The browser encryption public key.</summary>
    public required string P256dh { get; init; }
    /// <summary>The encryption authentication secret.</summary>
    public required string Auth { get; init; }
    /// <summary>The notification language.</summary>
    public string Language { get; init; } = "en";
}

/// <summary>The public application key for registering Web Push.</summary>
public sealed record PushKey(string PublicKey);
