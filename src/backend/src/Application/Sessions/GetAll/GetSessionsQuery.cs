using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Sessions;
using Domain.Shared;
using Response = Contracts.Sessions.GetAll.Response;

namespace Application.Sessions.GetAll;

/// <summary>Lists the caller's signed-in devices.</summary>
/// <param name="UserId">Whose sessions.</param>
/// <param name="CurrentSessionId">The session making the request, so it can be marked.</param>
public sealed record GetSessionsQuery(Guid UserId, Guid? CurrentSessionId);

internal sealed class GetSessionsQueryHandler(ISessionStore sessions)
    : IQueryHandler<GetSessionsQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetSessionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Sessions.GetAll");

        var found = await sessions.ForUserAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        return tracked.Record(Result<Response>.Success(found.ToGetAllResponse(query.CurrentSessionId)));
    }
}

/// <summary>Maps sessions onto the shape this operation returns.</summary>
internal static class SessionListMappings
{
    internal static Response ToGetAllResponse(
        this IReadOnlyList<Session> sessions,
        Guid? currentSessionId)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        return new Response
        {
            Items = [.. sessions.Select(session => session.ToSummary(currentSessionId))]
        };
    }

    private static Contracts.Sessions.GetAll.SessionSummary ToSummary(
        this Session session,
        Guid? currentSessionId) =>
        new()
        {
            SessionId = session.Id,
            CreatedAt = session.CreatedAt,
            LastSeenAt = session.LastSeenAt,
            IpAddress = session.IpAddress,
            UserAgent = session.UserAgent,
            IsCurrent = session.Id == currentSessionId
        };
}
