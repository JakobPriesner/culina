using Domain.Shared;

namespace Domain.Users;

/// <summary>What a link mailed to an account's address is for.</summary>
public enum EmailTokenPurpose
{
    /// <summary>Proves the address belongs to whoever registered with it.</summary>
    EmailConfirmation,

    /// <summary>Signs the owner in without a password.</summary>
    SignIn
}

/// <summary>
/// A single-use secret mailed to an account's address as a link; only its digest is stored, and opening the link
/// proves the opener reads that inbox.
/// </summary>
public sealed class EmailToken
{
    /// <summary>A confirmation link waits for somebody who may only look at their mail tomorrow.</summary>
    public static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromHours(48);

    /// <summary>A sign-in link is asked for by somebody waiting for it, and is as good as a password while it lives.</summary>
    public static readonly TimeSpan SignInLifetime = TimeSpan.FromMinutes(15);

    private EmailToken(
        Guid id,
        Guid userId,
        EmailTokenPurpose purpose,
        ReadOnlyMemory<byte> tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        Purpose = purpose;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>The token's id.</summary>
    public Guid Id { get; }

    /// <summary>Whose address it was mailed to.</summary>
    public Guid UserId { get; }

    /// <summary>What opening it does.</summary>
    public EmailTokenPurpose Purpose { get; }

    /// <summary>The digest of the token. The token itself is only ever in the mail.</summary>
    public ReadOnlyMemory<byte> TokenHash { get; }

    /// <summary>When it was made.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When it stops working.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>A new token for this purpose, living as long as the purpose allows.</summary>
    /// <param name="userId">Whose address.</param>
    /// <param name="purpose">What it is for.</param>
    /// <param name="tokenHash">The digest of the token.</param>
    /// <param name="now">The injected current time.</param>
    public static EmailToken Issue(Guid userId, EmailTokenPurpose purpose, ReadOnlyMemory<byte> tokenHash, DateTimeOffset now) =>
        new(
            CulinaId.New(),
            userId,
            purpose,
            tokenHash,
            now,
            now.Add(purpose == EmailTokenPurpose.SignIn ? SignInLifetime : ConfirmationLifetime));
}
