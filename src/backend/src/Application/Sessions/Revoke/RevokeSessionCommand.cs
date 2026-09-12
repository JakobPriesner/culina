using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;

namespace Application.Sessions.Revoke;

/// <summary>Ends one of the caller's sessions.</summary>
/// <param name="SessionId">Which session.</param>
/// <param name="UserId">Whose it must be.</param>
public sealed record RevokeSessionCommand(Guid SessionId, Guid UserId);

internal sealed class RevokeSessionCommandHandler(ISessionStore sessions, TimeProvider time)
    : ICommandHandler<RevokeSessionCommand>
{
    public async Task<Result> Handle(
        RevokeSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Sessions.Revoke");

        // The store scopes the update to the caller's own sessions in SQL, so
        // revoking someone else's is not merely refused — it is unexpressible.
        var result = await sessions
            .RevokeAsync(command.SessionId, command.UserId, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        return tracked.Record(result);
    }
}
