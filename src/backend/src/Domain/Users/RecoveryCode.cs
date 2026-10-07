using System.Text;
using Domain.Shared;

namespace Domain.Users;

/// <summary>
/// A one-time credential that sets a new password; only its digest is stored. A <em>saved</em> code is one of a
/// set the holder keeps and never expires; an <em>issued</em> code comes from the administrator and expires within a day.
/// </summary>
public sealed class RecoveryCode
{
    /// <summary>How many codes a saved set holds.</summary>
    public const int SetSize = 10;

    /// <summary>How long a code the administrator issued stays usable.</summary>
    public static readonly TimeSpan IssuedLifetime = TimeSpan.FromHours(24);

    private RecoveryCode(
        Guid id,
        Guid userId,
        ReadOnlyMemory<byte> codeHash,
        Guid? issuedBy,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt)
    {
        Id = id;
        UserId = userId;
        CodeHash = codeHash;
        IssuedBy = issuedBy;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>The code's id.</summary>
    public Guid Id { get; }

    /// <summary>Whose password it resets.</summary>
    public Guid UserId { get; }

    /// <summary>The digest of the normalised code. The code itself is shown once.</summary>
    public ReadOnlyMemory<byte> CodeHash { get; }

    /// <summary>The administrator who issued it, or null for one of the account's own saved set.</summary>
    public Guid? IssuedBy { get; }

    /// <summary>When it was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>When it stops working, or null for a saved code, which does not.</summary>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>One code of the account holder's own saved set.</summary>
    /// <param name="userId">Whose account.</param>
    /// <param name="codeHash">The digest of the normalised code.</param>
    /// <param name="now">The injected current time.</param>
    public static RecoveryCode Saved(Guid userId, ReadOnlyMemory<byte> codeHash, DateTimeOffset now) =>
        new(CulinaId.New(), userId, codeHash, issuedBy: null, now, expiresAt: null);

    /// <summary>A code the administrator issued for somebody locked out.</summary>
    /// <param name="userId">Whose account.</param>
    /// <param name="codeHash">The digest of the normalised code.</param>
    /// <param name="issuedBy">The administrator.</param>
    /// <param name="now">The injected current time.</param>
    public static RecoveryCode Issued(
        Guid userId,
        ReadOnlyMemory<byte> codeHash,
        Guid issuedBy,
        DateTimeOffset now) =>
        new(CulinaId.New(), userId, codeHash, issuedBy, now, now.Add(IssuedLifetime));

    /// <summary>
    /// The form a code is hashed in. Codes are typed from paper, so case, spaces and dashes are ignored
    /// and look-alike letters (O, I, L) read as digits.
    /// </summary>
    /// <param name="typed">What was entered.</param>
    public static string Normalise(string? typed)
    {
        var normalised = new StringBuilder(typed?.Length ?? 0);

        foreach (var character in typed ?? string.Empty)
        {
            if (character is ' ' or '-' or '\t')
            {
                continue;
            }

            normalised.Append(char.ToUpperInvariant(character) switch
            {
                'O' => '0',
                'I' or 'L' => '1',
                var other => other
            });
        }

        return normalised.ToString();
    }
}
