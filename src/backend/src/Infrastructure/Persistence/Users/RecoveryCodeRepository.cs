using Application.Abstractions;
using Domain.Sessions;
using Domain.Shared;
using Domain.Users;

namespace Infrastructure.Persistence.Users;

/// <summary>Stores recovery codes.</summary>
/// <param name="executor">Runs the SQL inside the request's transaction.</param>
internal sealed class RecoveryCodeRepository(DbExecutor executor) : IRecoveryCodeRepository
{
    public async Task ReplaceSavedAsync(
        Guid userId,
        IReadOnlyList<RecoveryCode> codes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codes);

        // Used codes of the old set go too: what is left of a set is its
        // remaining count, and a new set starts that count again.
        await executor.ExecuteAsync(
            "delete from recovery_codes where user_id = @userId and issued_by is null;",
            new { userId },
            cancellationToken).ConfigureAwait(false);

        if (codes.Count == 0)
        {
            return;
        }

        await executor.ExecuteAsync(
            """
            insert into recovery_codes (id, user_id, code_hash, issued_by, created_at, expires_at)
            select id, user_id, code_hash, issued_by, created_at, expires_at
            from unnest(
                @ids::uuid[], @userIds::uuid[], @codeHashes::bytea[], @issuedBys::uuid[],
                @createdAts::timestamptz[], @expiresAts::timestamptz[])
                as c(id, user_id, code_hash, issued_by, created_at, expires_at);
            """,
            new
            {
                ids = codes.Select(code => code.Id).ToArray(),
                userIds = codes.Select(code => code.UserId).ToArray(),
                codeHashes = codes.Select(code => code.CodeHash.ToArray()).ToArray(),
                issuedBys = codes.Select(code => code.IssuedBy).ToArray(),
                createdAts = codes.Select(code => code.CreatedAt).ToArray(),
                expiresAts = codes.Select(code => code.ExpiresAt).ToArray()
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(RecoveryCode code, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        await executor.ExecuteAsync(
            """
            insert into recovery_codes (id, user_id, code_hash, issued_by, created_at, expires_at)
            values (@id, @userId, @codeHash, @issuedBy, @createdAt, @expiresAt);
            """,
            new
            {
                id = code.Id,
                userId = code.UserId,
                codeHash = code.CodeHash.ToArray(),
                issuedBy = code.IssuedBy,
                createdAt = code.CreatedAt,
                expiresAt = code.ExpiresAt
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<SavedRecoveryCodes> SavedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<SavedRow>(
            """
            select count(*) filter (where used_at is null) as remaining, min(created_at) as created_at
            from recovery_codes
            where user_id = @userId and issued_by is null;
            """,
            new { userId },
            cancellationToken).ConfigureAwait(false);

        return new SavedRecoveryCodes((int)(row?.Remaining ?? 0), row?.CreatedAt);
    }

    public async Task<Result<Guid>> RedeemAsync(
        Email email,
        ReadOnlyMemory<byte> codeHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        // One statement for "this address, this code, still usable, now used",
        // so the work is the same whether the address is registered or not,
        // and two people racing the same code cannot both get through.
        var userId = await executor.ExecuteScalarAsync<Guid?>(
            """
            update recovery_codes code
            set used_at = @now
            from users
            where users.id = code.user_id
              and users.email = @email
              and code.code_hash = @codeHash
              and code.used_at is null
              and (code.expires_at is null or code.expires_at > @now)
            returning code.user_id;
            """,
            new { email = email.Value, codeHash = codeHash.ToArray(), now },
            cancellationToken).ConfigureAwait(false);

        return userId is { } found ? found : SessionErrors.InvalidRecoveryCode;
    }

    private sealed record SavedRow
    {
        public long Remaining { get; init; }

        public DateTimeOffset? CreatedAt { get; init; }
    }
}
