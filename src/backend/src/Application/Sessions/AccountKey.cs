using System.Security.Cryptography;
using System.Text;

namespace Application.Sessions;

/// <summary>
/// The key one account's failed attempts are counted under.
/// </summary>
/// <remarks>
/// Shared by everything that checks a secret of an account — signing in,
/// changing the password, redeeming a recovery code — so they draw on one
/// budget. Three separate budgets would be three times the guesses.
/// </remarks>
internal static class AccountKey
{
    /// <summary>
    /// A digest of the normalised address, because the attempt counter lives in
    /// memory and shows up in diagnostics, and neither should hold an email.
    /// </summary>
    /// <param name="rawEmail">The address as given.</param>
    internal static string For(string rawEmail) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawEmail.Trim().ToLowerInvariant())));
}
