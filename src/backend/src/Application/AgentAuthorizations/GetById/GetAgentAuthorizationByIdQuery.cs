using Application.Abstractions;
using Application.Abstractions.Messaging;
using Domain.AgentAuthorizations;
using Domain.Shared;
using Response = Contracts.AgentAuthorizations.GetById.Response;

namespace Application.AgentAuthorizations.GetById;

/// <summary>Reads a pending request to connect an AI agent, for the consent screen.</summary>
/// <param name="AuthorizationId">Which request.</param>
public sealed record GetAgentAuthorizationByIdQuery(Guid AuthorizationId);

internal sealed class GetAgentAuthorizationByIdQueryHandler(IAgentAuthorizationRepository authorizations, TimeProvider time)
    : IQueryHandler<GetAgentAuthorizationByIdQuery, Response>
{
    public async Task<Result<Response>> Handle(GetAgentAuthorizationByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var found = await authorizations.FindAsync(query.AuthorizationId, cancellationToken).ConfigureAwait(false);
        var now = time.GetUtcNow();

        // Answered or lapsed is the same as never asked: there is nothing left to decide.
        return found.Bind(authorization => authorization.IsPending(now)
            ? Result<Response>.Success(authorization.ToGetByIdResponse())
            : Result<Response>.Failure(AgentAuthorizationErrors.NotFound));
    }
}
