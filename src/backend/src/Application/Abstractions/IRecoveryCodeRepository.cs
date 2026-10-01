using Domain.Shared;
using Domain.Users;

namespace Application.Abstractions;

/// <summary>Reads and writes recovery codes.</summary>
public interface IRecoveryCodeRepository
{
    /// <summary>
    /// Replaces the account's saved set: every code of the old set stops
    /// working, used or not.
    /// </summary>
    /// <param name="userId">Whose set.</param>
    /// <param name="codes">The new set.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task ReplaceSavedAsync(Guid userId, IReadOnlyList<RecoveryCode> codes, CancellationToken cancellationToken);

    /// <summary>Stores a code the administrator issued.</summary>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task AddAsync(RecoveryCode code, CancellationToken cancellationToken);

    /// <summary>How many codes of the saved set are left, and when the set was made.</summary>
    /// <param name="userId">Whose set.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<SavedRecoveryCodes> SavedAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Uses up the code, if it belongs to the account with that address and is
    /// still usable, in one statement.
    /// </summary>
    /// <param name="email">The address the person gave.</param>
    /// <param name="codeHash">The digest of the normalised code.</param>
    /// <param name="now">The injected current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whose account it unlocked, or <c>auth.invalid_recovery_code</c>.</returns>
    Task<Result<Guid>> RedeemAsync(
        Email email,
        ReadOnlyMemory<byte> codeHash,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

/// <summary>What is left of an account's saved set.</summary>
/// <param name="Remaining">Codes not yet used.</param>
/// <param name="CreatedAt">When the set was made, or null when there is none.</param>
public sealed record SavedRecoveryCodes(int Remaining, DateTimeOffset? CreatedAt);
