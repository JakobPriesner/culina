using Application.Abstractions;
using Domain.AgentAuthorizations;
using Domain.Shared;

namespace Infrastructure.Persistence.AgentAuthorizations;

/// <summary>Stores requests to connect an AI agent, from asking to redeeming the code.</summary>
internal sealed class AgentAuthorizationRepository(DbExecutor executor) : IAgentAuthorizationRepository
{
    private const string Columns =
        "id, client_id, client_name, client_host, redirect_uri, code_challenge, state, resource, created_at, "
        + "expires_at, user_id, decided_at, approved, code_hash, redeemed_at, session_id, "
        + "host(approver_ip_address) as approver_ip_address, approver_user_agent";

    /// <summary>How long a request is kept past its deadline, so a code presented again still ends its session.</summary>
    private static readonly TimeSpan KeptFor = TimeSpan.FromDays(1);

    public Task AddAsync(AgentAuthorization authorization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return executor.ExecuteAsync(
            """
            delete from agent_authorizations where expires_at < @keptSince;

            insert into agent_authorizations (id, client_id, client_name, client_host, redirect_uri, code_challenge,
                                              state, resource, created_at, expires_at)
            values (@id, @clientId, @clientName, @clientHost, @redirectUri, @codeChallenge,
                    @state, @resource, @createdAt, @expiresAt);
            """,
            new
            {
                id = authorization.Id,
                clientId = authorization.Client.ClientId,
                clientName = authorization.Client.Name,
                clientHost = authorization.Client.VerifiedHost,
                redirectUri = authorization.RedirectUri,
                codeChallenge = authorization.CodeChallenge,
                state = authorization.State,
                resource = authorization.Resource,
                createdAt = authorization.CreatedAt,
                expiresAt = authorization.ExpiresAt,
                keptSince = authorization.CreatedAt - KeptFor
            },
            cancellationToken);
    }

    public async Task<Result<AgentAuthorization>> FindAsync(Guid authorizationId, CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<AgentAuthorizationRow>(
            $"select {Columns} from agent_authorizations where id = @authorizationId;",
            new { authorizationId },
            cancellationToken).ConfigureAwait(false);

        return row is null ? AgentAuthorizationErrors.NotFound : row.ToDomain();
    }

    public async Task<Result<AgentAuthorization>> FindByCodeAsync(
        ReadOnlyMemory<byte> codeHash,
        CancellationToken cancellationToken)
    {
        // By digest, so the code itself never reaches an index, a log or a plan.
        var row = await executor.QuerySingleOrDefaultAsync<AgentAuthorizationRow>(
            $"select {Columns} from agent_authorizations where code_hash = @codeHash;",
            new { codeHash = codeHash.ToArray() },
            cancellationToken).ConfigureAwait(false);

        return row is null ? AgentAuthorizationErrors.InvalidGrant : row.ToDomain();
    }

    public async Task<Result> DecideAsync(AgentAuthorization authorization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        // Only an unanswered request: of two answers at once, the second finds nothing to change.
        var affected = await executor.ExecuteAsync(
            """
            update agent_authorizations
            set user_id = @userId,
                decided_at = @decidedAt,
                approved = @approved,
                code_hash = @codeHash,
                expires_at = @expiresAt,
                approver_ip_address = cast(@ipAddress as inet),
                approver_user_agent = @userAgent
            where id = @id and decided_at is null;
            """,
            new
            {
                id = authorization.Id,
                userId = authorization.UserId,
                decidedAt = authorization.DecidedAt,
                approved = authorization.Approved,
                codeHash = authorization.CodeHash?.ToArray(),
                expiresAt = authorization.ExpiresAt,
                ipAddress = authorization.ApproverIpAddress,
                userAgent = Truncate(authorization.ApproverUserAgent)
            },
            cancellationToken).ConfigureAwait(false);

        return affected == 0 ? AgentAuthorizationErrors.NotFound : Result.Success();
    }

    public async Task<Result> RedeemAsync(AgentAuthorization authorization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        // Only a code nobody redeemed yet: two exchanges racing cannot both start a session.
        var affected = await executor.ExecuteAsync(
            """
            update agent_authorizations
            set redeemed_at = @redeemedAt, session_id = @sessionId
            where id = @id and redeemed_at is null;
            """,
            new { id = authorization.Id, redeemedAt = authorization.RedeemedAt, sessionId = authorization.SessionId },
            cancellationToken).ConfigureAwait(false);

        return affected == 0 ? AgentAuthorizationErrors.InvalidGrant : Result.Success();
    }

    // The same bound the session keeps, which this becomes.
    private static string? Truncate(string? userAgent) =>
        userAgent is null ? null : userAgent[..Math.Min(userAgent.Length, 400)];

    private sealed record AgentAuthorizationRow
    {
        public Guid Id { get; init; }

        public string ClientId { get; init; } = string.Empty;

        public string ClientName { get; init; } = string.Empty;

        public string? ClientHost { get; init; }

        public string RedirectUri { get; init; } = string.Empty;

        public string CodeChallenge { get; init; } = string.Empty;

        public string? State { get; init; }

        public string Resource { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset ExpiresAt { get; init; }

        public Guid? UserId { get; init; }

        public DateTimeOffset? DecidedAt { get; init; }

        public bool Approved { get; init; }

        public byte[]? CodeHash { get; init; }

        public DateTimeOffset? RedeemedAt { get; init; }

        public Guid? SessionId { get; init; }

        public string? ApproverIpAddress { get; init; }

        public string? ApproverUserAgent { get; init; }

        public AgentAuthorization ToDomain() =>
            AgentAuthorization.Restore(
                Id,
                new AgentClient(ClientId, ClientName, ClientHost),
                RedirectUri,
                CodeChallenge,
                State,
                Resource,
                CreatedAt,
                ExpiresAt,
                UserId,
                DecidedAt,
                Approved,
                // Spelled out: a bare null would convert to an empty ReadOnlyMemory, which reads as a code.
                CodeHash is { } hash ? new ReadOnlyMemory<byte>(hash) : (ReadOnlyMemory<byte>?)null,
                RedeemedAt,
                SessionId,
                ApproverIpAddress,
                ApproverUserAgent);
    }
}
