using Application.Abstractions;
using Domain.Shared;
using Domain.Users;

namespace Application.Mail;

/// <summary>Finds the account an address belongs to for the mails anybody may ask for, where every miss looks the same.</summary>
internal static class AccountLookup
{
    /// <summary>The account, or null for both "not an address" and "no such account".</summary>
    internal static async Task<User?> FindByAddressAsync(this IUserRepository users, string rawEmail, CancellationToken cancellationToken)
    {
        var found = await Email.Create(rawEmail).Match(
            email => users.FindByEmailAsync(email, cancellationToken),
            error => Task.FromResult(Result<User>.Failure(error))).ConfigureAwait(false);

        return found.Match<User?>(user => user, _ => null);
    }
}
