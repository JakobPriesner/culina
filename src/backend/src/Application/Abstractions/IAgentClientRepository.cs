using Domain.AgentAuthorizations;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Apps that registered themselves to connect AI agents (OAuth dynamic client registration, RFC 7591).</summary>
public interface IAgentClientRepository
{
    /// <summary>Stores a newly registered app, and forgets apps nobody used.</summary>
    /// <param name="registration">The app and its return addresses.</param>
    /// <param name="now">The injected current time: when it registered.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task AddAsync(ClientRegistration registration, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>The app with this <c>client_id</c>.</summary>
    /// <returns><c>oauth.invalid_client</c> when Culina never issued it, or forgot it.</returns>
    Task<Result<ClientRegistration>> FindAsync(string clientId, CancellationToken cancellationToken);

    /// <summary>Notes that an app connected someone, which keeps it registered.</summary>
    Task UsedAsync(string clientId, DateTimeOffset now, CancellationToken cancellationToken);
}
