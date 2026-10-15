using Application.Abstractions;
using Application.Abstractions.Messaging;
using Contracts.Recipes.Intake;
using Domain.Shared;

namespace Application.Recipes.Intake;

/// <summary>Asks for a failed import to be tried again as a new job.</summary>
/// <param name="Id">The failed job.</param>
/// <param name="NextId">The id the caller picked for the new one, so asking twice is one retry.</param>
/// <param name="UserId">Whose import.</param>
public sealed record RetryRecipeIntakeCommand(Guid Id, Guid NextId, Guid UserId);

/// <summary>Queues the new job and dismisses the failed one in one transaction, so two retries cannot both succeed.</summary>
internal sealed class RetryRecipeIntakeCommandHandler(
    IRecipeIntakeJobs jobs,
    IUnitOfWork transactions,
    IntakeSubmission submission)
    : ICommandHandler<RetryRecipeIntakeCommand, IntakeJob>
{
    public async Task<Result<IntakeJob>> Handle(RetryRecipeIntakeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var already = await jobs.GetAsync(command.NextId, command.UserId, cancellationToken).ConfigureAwait(false);

        if (already is not null)
        {
            return already;
        }

        var failed = await jobs.GetAsync(command.Id, command.UserId, cancellationToken).ConfigureAwait(false);
        var original = failed is { Stage: "failed" }
            ? await jobs.MaterialAsync(command.Id, command.UserId, cancellationToken).ConfigureAwait(false)
            : null;

        if (failed is null || original is null)
        {
            return IntakeErrors.NotFound;
        }

        var allowed = await submission
            .CheckAsync(command.UserId, failed.HouseholdId, command.NextId, original, cancellationToken)
            .ConfigureAwait(false);

        var retried = await allowed.Match(
            () => transactions.InTransactionAsync(
                token => RetryAsync(command, failed.HouseholdId, original, token),
                cancellationToken),
            error => Task.FromResult(Result<IntakeJob>.Failure(error))).ConfigureAwait(false);

        return retried.Tap(job => submission.Announce(command.UserId, job));
    }

    // Queuing takes the person's lock, so a second retry waits here and then finds that the first one dismissed the
    // old job: it fails, and its transaction takes the new job back with it.
    private async Task<Result<IntakeJob>> RetryAsync(
        RetryRecipeIntakeCommand command,
        Guid householdId,
        IntakeMaterial original,
        CancellationToken token)
    {
        var queued = await jobs.EnqueueAsync(command.NextId, command.UserId, householdId, original, token).ConfigureAwait(false);

        return await queued.Match(
            async next =>
            {
                var current = await jobs.GetAsync(command.Id, command.UserId, token).ConfigureAwait(false);

                if (current is not { Stage: "failed" })
                {
                    return Result<IntakeJob>.Failure(IntakeErrors.NotFound);
                }

                await jobs.ReviewAsync(command.Id, command.UserId, token).ConfigureAwait(false);

                return Result<IntakeJob>.Success(next);
            },
            error => Task.FromResult(Result<IntakeJob>.Failure(error))).ConfigureAwait(false);
    }
}
