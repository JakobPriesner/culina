using Domain.Users;

namespace Infrastructure.Persistence.Users;

/// <summary>Turns a stored row back into a domain user.</summary>
internal static class UserRowMappings
{
    internal static User ToDomain(this UserRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        // The values came from this application, so a row that no longer parses
        // is a defect in the data rather than an expected outcome — it throws
        // rather than returning a Result nobody could act on.
        var email = row.Email.ToEmailOrThrow();
        var displayName = row.DisplayName.ToDisplayNameOrThrow();

        return User.Restore(row.Id, email, displayName, row.PasswordHash, row.CreatedAt, row.Version);
    }

    private static Email ToEmailOrThrow(this string value) =>
        Email.Create(value).Match(
            email => email,
            error => throw new InvalidOperationException(
                $"Stored email '{value}' is not valid ({error.Code}). The row is corrupt."));

    private static DisplayName ToDisplayNameOrThrow(this string value) =>
        DisplayName.Create(value).Match(
            name => name,
            error => throw new InvalidOperationException(
                $"Stored display name is not valid ({error.Code}). The row is corrupt."));
}
