using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Cooking;
using Domain.Shared;

namespace Application.CookSessions;

/// <summary>Finishes a session, or gives up on it.</summary>
/// <param name="SessionId">Which session.</param>
/// <param name="UserId">Whose it must be.</param>
/// <param name="Completed">
/// True when the cooking was finished, false when it was abandoned. The
/// difference is worth keeping: one of them means the recipe worked.
/// </param>
public sealed record EndCookSessionCommand(Guid SessionId, Guid UserId, bool Completed);

internal sealed class EndCookSessionCommandHandler(
    ICookSessionRepository sessions,
    TimeProvider time)
    : ICommandHandler<EndCookSessionCommand>
{
    public async Task<Result> Handle(
        EndCookSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("CookSessions.End");

        var found = await sessions
            .FindAsync(command.SessionId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            session => EndAsync(session, command.Completed, cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result> EndAsync(
        CookSession session,
        bool completed,
        CancellationToken cancellationToken)
    {
        var before = session.Version;
        var now = time.GetUtcNow();

        if (completed)
        {
            var finished = session.Complete(now);

            if (finished.Match(() => false, _ => true))
            {
                return finished;
            }
        }
        else
        {
            // Abandoning something already over is not an error; see Abandon.
            session.Abandon(now);

            if (session.Version == before)
            {
                return Result.Success();
            }
        }

        return await sessions.UpdateAsync(session, before, cancellationToken).ConfigureAwait(false);
    }
}
