using Contracts.Recipes.Intake;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>The material retained until an import has been reviewed.</summary>
public sealed record IntakeMaterial(string Text, string Transcript, string? SourceUrl,
    string Language, IReadOnlyList<IntakePhoto> Photos, bool FetchSource = false);

/// <summary>An original screenshot, bounded and signature checked on intake.</summary>
public sealed record IntakePhoto(ReadOnlyMemory<byte> Bytes, string MediaType);

/// <summary>A claimed job, with a lease preventing concurrent workers.</summary>
public sealed record IntakeWork(Guid Id, Guid UserId, Guid HouseholdId, IntakeMaterial Material,
    Contracts.Recipes.Drafts.Response? Draft);

/// <summary>Durable server work, independent of a browser connection.</summary>
public interface IRecipeIntakeJobs
{
    /// <summary>Accepts an idempotent submission, subject to a per-user queue cap.</summary>
    Task<Result<IntakeJob>> EnqueueAsync(Guid id, Guid userId, Guid householdId, IntakeMaterial material, CancellationToken token);
    /// <summary>Reads the user's unfinished or unreviewed jobs.</summary>
    Task<IReadOnlyList<IntakeJob>> ListAsync(Guid userId, CancellationToken token);
    /// <summary>Reads one user's import.</summary>
    Task<IntakeJob?> GetAsync(Guid id, Guid userId, CancellationToken token);
    /// <summary>Reads retained original material.</summary>
    Task<IntakeMaterial?> MaterialAsync(Guid id, Guid userId, CancellationToken token);
    /// <summary>Retains the source fetched by the server for later comparison.</summary>
    Task SourceAsync(Guid id, IntakeMaterial material, CancellationToken token);
    /// <summary>Marks a ready recipe reviewed and releases its source images.</summary>
    Task ReviewAsync(Guid id, Guid userId, CancellationToken token);
    /// <summary>Claims queued work or a lease abandoned by a stopped server.</summary>
    Task<IntakeWork?> ClaimAsync(CancellationToken token);
    /// <summary>Persists progress and the latest draft.</summary>
    Task ProgressAsync(Guid id, string stage, Contracts.Recipes.Drafts.Response? draft, CancellationToken token);
    /// <summary>Completes inside the recipe's database transaction.</summary>
    Task CompleteAsync(Guid id, Guid recipeId, CancellationToken token);
    /// <summary>Records an expected failure.</summary>
    Task FailAsync(Guid id, string errorCode, CancellationToken token);
}

/// <summary>Notification registration and durable delivery.</summary>
public interface IIntakeNotifications
{
    /// <summary>The persisted VAPID public key.</summary>
    Task<string> PublicKeyAsync(CancellationToken token);
    /// <summary>Registers only a supported public push service endpoint.</summary>
    Task<Result> RegisterAsync(Guid userId, PushRegistration subscription, CancellationToken token);
    /// <summary>Removes this device's subscription.</summary>
    Task RemoveAsync(Guid userId, string endpoint, CancellationToken token);
    /// <summary>Delivers pending notifications only for already saved recipes.</summary>
    Task DeliverAsync(CancellationToken token);
}
