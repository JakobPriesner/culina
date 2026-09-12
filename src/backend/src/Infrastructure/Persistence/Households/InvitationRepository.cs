using Application.Abstractions;
using Domain.Households;
using Domain.Shared;

namespace Infrastructure.Persistence.Households;

/// <summary>The <c>household_invitations</c> row as PostgreSQL returns it.</summary>
internal sealed record InvitationRow
{
    public Guid Id { get; init; }

    public Guid HouseholdId { get; init; }

    public byte[] CodeHash { get; init; } = [];

    public Guid CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public Guid? RedeemedBy { get; init; }

    public DateTimeOffset? RedeemedAt { get; init; }
}

/// <summary>Stores household invitations.</summary>
/// <param name="executor">Runs the SQL.</param>
/// <param name="tokens">Hashes the code for lookup.</param>
internal sealed class InvitationRepository(DbExecutor executor, ISecretTokens tokens)
    : IInvitationRepository
{
    private const string Columns =
        "id, household_id, code_hash, created_by, created_at, expires_at, redeemed_by, redeemed_at";

    public async Task<Result<HouseholdInvitation>> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<InvitationRow>(
            $"select {Columns} from household_invitations where code_hash = @codeHash;",
            new { codeHash = tokens.Digest(code).ToArray() },
            cancellationToken).ConfigureAwait(false);

        return row is null ? HouseholdErrors.InvitationInvalid : row.ToDomain();
    }

    public async Task<IReadOnlyList<HouseholdInvitation>> OpenForHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync<InvitationRow>(
            $"""
             select {Columns} from household_invitations
             where household_id = @householdId and redeemed_at is null
             order by created_at desc;
             """,
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => row.ToDomain())];
    }

    public async Task<Result> AddAsync(
        HouseholdInvitation invitation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        await executor.ExecuteAsync(
            """
            insert into household_invitations
                (id, household_id, code_hash, created_by, created_at, expires_at)
            values (@id, @householdId, @codeHash, @createdBy, @createdAt, @expiresAt);
            """,
            new
            {
                id = invitation.Id,
                householdId = invitation.HouseholdId,
                codeHash = invitation.CodeHash.ToArray(),
                createdBy = invitation.CreatedBy,
                createdAt = invitation.CreatedAt,
                expiresAt = invitation.ExpiresAt
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> MarkRedeemedAsync(
        HouseholdInvitation invitation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        // `redeemed_at is null` in the WHERE is what makes an invitation
        // single-use under concurrency: two requests presenting the same code
        // race here, and the database picks one.
        var affected = await executor.ExecuteAsync(
            """
            update household_invitations
            set redeemed_by = @redeemedBy, redeemed_at = @redeemedAt
            where id = @id and redeemed_at is null;
            """,
            new
            {
                id = invitation.Id,
                redeemedBy = invitation.RedeemedBy,
                redeemedAt = invitation.RedeemedAt
            },
            cancellationToken).ConfigureAwait(false);

        return affected == 0 ? HouseholdErrors.InvitationInvalid : Result.Success();
    }

    public async Task<Result> RevokeAsync(
        Guid invitationId,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var affected = await executor.ExecuteAsync(
            "delete from household_invitations where id = @invitationId and household_id = @householdId;",
            new { invitationId, householdId },
            cancellationToken).ConfigureAwait(false);

        return affected == 0 ? HouseholdErrors.InvitationInvalid : Result.Success();
    }
}

/// <summary>Turns a stored row back into a domain invitation.</summary>
internal static class InvitationRowMappings
{
    internal static HouseholdInvitation ToDomain(this InvitationRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return HouseholdInvitation.Restore(
            row.Id,
            row.HouseholdId,
            row.CodeHash,
            row.CreatedBy,
            row.CreatedAt,
            row.ExpiresAt,
            row.RedeemedBy,
            row.RedeemedAt);
    }
}
