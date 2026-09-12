using Domain.Shared;

namespace Domain.Households;

/// <summary>
/// A one-time code that lets someone join a household.
/// </summary>
/// <remarks>
/// The code is a credential: anyone holding it can join. Only its digest is
/// stored, it works once, and it expires — three independent limits, so a code
/// that leaks from a chat log has a small window rather than an open door.
/// </remarks>
public sealed class HouseholdInvitation
{
    /// <summary>How long an invitation lives by default.</summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(14);

    private HouseholdInvitation(
        Guid id,
        Guid householdId,
        ReadOnlyMemory<byte> codeHash,
        Guid createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        Guid? redeemedBy,
        DateTimeOffset? redeemedAt)
    {
        Id = id;
        HouseholdId = householdId;
        CodeHash = codeHash;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        RedeemedBy = redeemedBy;
        RedeemedAt = redeemedAt;
    }

    /// <summary>The invitation's id, used to revoke it.</summary>
    public Guid Id { get; }

    /// <summary>Which household it admits to.</summary>
    public Guid HouseholdId { get; }

    /// <summary>The digest of the code. The code itself is shown once.</summary>
    public ReadOnlyMemory<byte> CodeHash { get; }

    /// <summary>Which owner issued it.</summary>
    public Guid CreatedBy { get; }

    /// <summary>When it was issued.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When it stops working.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Who used it, if anyone.</summary>
    public Guid? RedeemedBy { get; private set; }

    /// <summary>When it was used.</summary>
    public DateTimeOffset? RedeemedAt { get; private set; }

    /// <summary>Issues an invitation.</summary>
    /// <param name="householdId">Which household it admits to.</param>
    /// <param name="codeHash">The digest of the generated code.</param>
    /// <param name="createdBy">Which owner issued it.</param>
    /// <param name="now">The injected current time.</param>
    /// <param name="lifetime">How long it should live.</param>
    public static HouseholdInvitation Issue(
        Guid householdId,
        ReadOnlyMemory<byte> codeHash,
        Guid createdBy,
        DateTimeOffset now,
        TimeSpan? lifetime = null) =>
        new(
            CulinaId.New(),
            householdId,
            codeHash,
            createdBy,
            now,
            now.Add(lifetime ?? DefaultLifetime),
            redeemedBy: null,
            redeemedAt: null);

    /// <summary>Rebuilds an invitation from storage.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="householdId">Which household it admits to.</param>
    /// <param name="codeHash">The digest of its code.</param>
    /// <param name="createdBy">Who issued it.</param>
    /// <param name="createdAt">When it was issued.</param>
    /// <param name="expiresAt">When it stops working.</param>
    /// <param name="redeemedBy">Who used it.</param>
    /// <param name="redeemedAt">When it was used.</param>
    public static HouseholdInvitation Restore(
        Guid id,
        Guid householdId,
        ReadOnlyMemory<byte> codeHash,
        Guid createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        Guid? redeemedBy,
        DateTimeOffset? redeemedAt) =>
        new(id, householdId, codeHash, createdBy, createdAt, expiresAt, redeemedBy, redeemedAt);

    /// <summary>Whether this invitation can still be used.</summary>
    /// <param name="now">The injected current time.</param>
    public bool IsUsable(DateTimeOffset now) => RedeemedBy is null && ExpiresAt > now;

    /// <summary>Marks the invitation used.</summary>
    /// <param name="userId">Who used it.</param>
    /// <param name="now">The injected current time.</param>
    /// <returns>
    /// <c>households.invitation_invalid</c> when it is expired or already used
    /// — the same error an unknown code gets, so a code cannot be probed for
    /// validity.
    /// </returns>
    public Result Redeem(Guid userId, DateTimeOffset now)
    {
        if (!IsUsable(now))
        {
            return HouseholdErrors.InvitationInvalid;
        }

        RedeemedBy = userId;
        RedeemedAt = now;

        return Result.Success();
    }
}
