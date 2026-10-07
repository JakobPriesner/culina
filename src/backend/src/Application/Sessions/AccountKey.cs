using System.Security.Cryptography;
using System.Text;

namespace Application.Sessions;

/// <summary>The key one account's failed attempts are counted under, shared by every secret check (sign-in, password change, recovery code) so they draw on one budget.</summary>
internal static class AccountKey
{
    /// <summary>A digest of the normalised address, since the in-memory counter and diagnostics should not hold an email.</summary>
    internal static string For(string rawEmail) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawEmail.Trim().ToLowerInvariant())));
}
