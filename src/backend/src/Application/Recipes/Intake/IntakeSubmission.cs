using Application.Abstractions;
using Application.Households;
using Application.Recipes.Drafts;
using Contracts.Recipes.Intake;
using Domain.Import;
using Domain.Shared;

namespace Application.Recipes.Intake;

/// <summary>What starting an import or a draft has in common: check the ask, queue the job, wake the worker.</summary>
internal sealed class IntakeSubmission(
    IRecipeIntakeJobs jobs,
    IHouseholdRepository households,
    IUnitOfWork transactions,
    IntakeChanges changes)
{
    /// <summary>Checks access and input before retaining work, then queues it.</summary>
    internal async Task<Result<IntakeJob>> StartAsync(
        Guid id,
        Guid userId,
        Guid householdId,
        IntakeMaterial material,
        CancellationToken token)
    {
        var checkedMaterial = await CheckAsync(userId, householdId, id, material, token).ConfigureAwait(false);

        return await checkedMaterial.Match(
            () => QueueAsync(id, userId, householdId, material, token),
            error => Task.FromResult(Result<IntakeJob>.Failure(error))).ConfigureAwait(false);
    }

    /// <summary>Queues a job that was already checked, and says so once it is committed, so a watcher that re-reads finds it.</summary>
    internal async Task<Result<IntakeJob>> QueueAsync(
        Guid id,
        Guid userId,
        Guid householdId,
        IntakeMaterial material,
        CancellationToken token)
    {
        var queued = await transactions
            .InTransactionAsync(t => jobs.EnqueueAsync(id, userId, householdId, material, t), token)
            .ConfigureAwait(false);

        return queued.Tap(job => Announce(userId, job));
    }

    /// <summary>Tells watchers and the idle worker about a job that is now committed.</summary>
    internal void Announce(Guid userId, IntakeJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        changes.Changed(userId, job.Id);
        changes.Queued();
    }

    /// <summary>Whether this person may ask this household, and whether the material is something to work from.</summary>
    internal async Task<Result> CheckAsync(
        Guid userId,
        Guid householdId,
        Guid id,
        IntakeMaterial material,
        CancellationToken token)
    {
        if (id == Guid.Empty || material.Text.Length + material.Transcript.Length > DraftLimits.MaxMaterialCharacters
            || material.Photos.Count > DraftLimits.MaxPhotos
            || material.Photos.Sum(photo => photo.Bytes.Length) > DraftLimits.MaxPhotoBytes)
        {
            return Domain.Assistance.AssistanceErrors.TooMuchToWorkFrom;
        }

        var nothing = string.IsNullOrWhiteSpace(material.Text) && string.IsNullOrWhiteSpace(material.Transcript)
            && material.Photos.Count == 0 && !material.FetchSource;

        if (nothing)
        {
            return Domain.Assistance.AssistanceErrors.NothingToWorkFrom;
        }

        var unreachable = (material.FetchSource && material.SourceUrl is null)
            || (material.SourceUrl is not null && SourceUrl.From(material.SourceUrl) is null);

        if (unreachable)
        {
            return ImportErrors.UnreachableAddress;
        }

        var access = await HouseholdAccess.MemberOfAsync(households, householdId, userId, token).ConfigureAwait(false);

        return access.Bind(() => RecipeWords.ToLanguage(material.Language).Match(_ => Result.Success(), Result.Failure));
    }
}
