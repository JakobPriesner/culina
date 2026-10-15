namespace Application.Abstractions;

/// <summary>Deletes finished intake work, so pasted text, transcripts, drafts and push outbox rows are not kept for ever.</summary>
public interface IIntakePurge
{
    /// <summary>Removes reviewed or failed jobs (with their photos) and spent push outbox rows that are old enough.</summary>
    Task<IntakePurged> PurgeAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>What one purge removed.</summary>
public sealed record IntakePurged(int Jobs, int Notifications);
