using Application.Abstractions;
using Domain.AgentAuthorizations;
using Domain.Shared;

namespace Infrastructure.Persistence.AgentAuthorizations;

/// <summary>Stores apps that registered themselves to connect AI agents.</summary>
internal sealed class AgentClientRepository(DbExecutor executor) : IAgentClientRepository
{
    /// <summary>How long an app that registered may wait before connecting anybody.</summary>
    /// <remarks>
    /// Registration is anonymous and Claude registers afresh for every new connection, so the apps that never got as
    /// far as a person approving them would otherwise pile up.
    /// </remarks>
    private static readonly TimeSpan UnusedFor = TimeSpan.FromDays(1);

    /// <summary>How long an app stays registered after it last connected somebody: as long as that session can live.</summary>
    private static readonly TimeSpan IdleFor = TimeSpan.FromDays(365);

    public Task AddAsync(ClientRegistration registration, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return executor.ExecuteAsync(
            """
            delete from agent_clients
            where (last_used_at is null and created_at < @unusedSince) or last_used_at < @idleSince;

            insert into agent_clients (id, name, redirect_uris, created_at)
            values (@id, @name, @redirectUris, @now);
            """,
            new
            {
                id = registration.Client.ClientId,
                name = registration.Client.Name,
                redirectUris = registration.RedirectUris.ToArray(),
                now,
                unusedSince = now - UnusedFor,
                idleSince = now - IdleFor
            },
            cancellationToken);
    }

    public async Task<Result<ClientRegistration>> FindAsync(string clientId, CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<AgentClientRow>(
            "select id, name, redirect_uris from agent_clients where id = @clientId;",
            new { clientId },
            cancellationToken).ConfigureAwait(false);

        return row is null
            ? AgentAuthorizationErrors.InvalidClient("no app registered with this client_id, so register it again")
            : new ClientRegistration(new AgentClient(row.Id, row.Name, VerifiedHost: null), row.RedirectUris);
    }

    public Task UsedAsync(string clientId, DateTimeOffset now, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "update agent_clients set last_used_at = @now where id = @clientId;",
            new { clientId, now },
            cancellationToken);

    private sealed record AgentClientRow
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string[] RedirectUris { get; init; } = [];
    }
}
