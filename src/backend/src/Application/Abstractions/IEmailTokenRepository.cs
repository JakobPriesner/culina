using Domain.Shared;
using Domain.Users;

namespace Application.Abstractions;

/// <summary>Reads and writes the single-use tokens mailed as links.</summary>
public interface IEmailTokenRepository
{
    /// <summary>Stores a token, and forgets the account's expired ones.</summary>
    /// <param name="token">The token.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task AddAsync(EmailToken token, CancellationToken cancellationToken);

    /// <summary>Atomically uses up a token if it is for this purpose and still usable.</summary>
    /// <param name="tokenHash">The digest of the token as presented.</param>
    /// <param name="purpose">What the caller wants to do with it.</param>
    /// <param name="now">The injected current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whose address it was mailed to, or <c>auth.invalid_link</c>.</returns>
    Task<Result<Guid>> RedeemAsync(
        ReadOnlyMemory<byte> tokenHash,
        EmailTokenPurpose purpose,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
