using Application.Abstractions;
using Domain.Sessions;
using Domain.Shared;
using Domain.Users;

namespace Infrastructure.Persistence.Users;

/// <summary>Stores the tokens mailed as links.</summary>
internal sealed class EmailTokenRepository(DbExecutor executor) : IEmailTokenRepository
{
    public async Task AddAsync(EmailToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        // Expired links of this account go first, so the table holds only what could still be opened.
        await executor.ExecuteAsync(
            """
            delete from email_tokens where user_id = @userId and expires_at <= @createdAt;

            insert into email_tokens (id, user_id, purpose, token_hash, created_at, expires_at)
            values (@id, @userId, @purpose, @tokenHash, @createdAt, @expiresAt);
            """,
            new
            {
                id = token.Id,
                userId = token.UserId,
                purpose = Code(token.Purpose),
                tokenHash = token.TokenHash.ToArray(),
                createdAt = token.CreatedAt,
                expiresAt = token.ExpiresAt
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<Guid>> RedeemAsync(
        ReadOnlyMemory<byte> tokenHash,
        EmailTokenPurpose purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // One statement for "this token, for this, still usable, now used", so a link opened twice at once works once.
        var userId = await executor.ExecuteScalarAsync<Guid?>(
            """
            update email_tokens
            set used_at = @now
            where token_hash = @tokenHash
              and purpose = @purpose
              and used_at is null
              and expires_at > @now
            returning user_id;
            """,
            new { tokenHash = tokenHash.ToArray(), purpose = Code(purpose), now },
            cancellationToken).ConfigureAwait(false);

        return userId is { } found ? found : SessionErrors.InvalidLink;
    }

    private static string Code(EmailTokenPurpose purpose) => purpose switch
    {
        EmailTokenPurpose.SignIn => "sign_in",
        _ => "email_confirmation"
    };
}
