using Domain.AgentAuthorizations;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Requests to connect an AI agent through OAuth, from asking to redeeming the code.</summary>
public interface IAgentAuthorizationRepository
{
    /// <summary>Stores a new request, and forgets ones long expired.</summary>
    Task AddAsync(AgentAuthorization authorization, CancellationToken cancellationToken);

    /// <summary>The request with this id.</summary>
    /// <returns><c>agent_authorizations.not_found</c> when there is none.</returns>
    Task<Result<AgentAuthorization>> FindAsync(Guid authorizationId, CancellationToken cancellationToken);

    /// <summary>The request whose authorization code has this digest.</summary>
    /// <returns><c>oauth.invalid_grant</c> when no code has it.</returns>
    Task<Result<AgentAuthorization>> FindByCodeAsync(ReadOnlyMemory<byte> codeHash, CancellationToken cancellationToken);

    /// <summary>Saves the person's answer, unless somebody answered first.</summary>
    /// <returns><c>agent_authorizations.not_found</c> when it was answered meanwhile.</returns>
    Task<Result> DecideAsync(AgentAuthorization authorization, CancellationToken cancellationToken);

    /// <summary>Saves that the code was redeemed and what session it became, unless it was redeemed first.</summary>
    /// <returns><c>oauth.invalid_grant</c> when another request redeemed it meanwhile.</returns>
    Task<Result> RedeemAsync(AgentAuthorization authorization, CancellationToken cancellationToken);
}
