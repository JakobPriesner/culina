namespace Infrastructure.Persistence.Users;

/// <summary>The <c>users</c> row exactly as PostgreSQL returns it; never leaves Infrastructure.</summary>
internal sealed record UserRow
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public long Version { get; init; }
}
