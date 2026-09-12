namespace Infrastructure.Persistence.Users;

/// <summary>
/// The <c>users</c> row exactly as PostgreSQL returns it.
/// </summary>
/// <remarks>
/// Never leaves Infrastructure. An Application signature mentioning a row type
/// would let the persistence shape dictate the domain's.
/// </remarks>
internal sealed record UserRow
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public long Version { get; init; }
}
