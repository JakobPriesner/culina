using Domain.Shared;

namespace Domain.Users;

/// <summary>
/// A person with an account; state changes go through methods, so an invariant cannot be broken by
/// assigning a property.
/// </summary>
public sealed class User
{
    /// <summary>
    /// The shortest password the app accepts; length is the only rule, as composition rules breed
    /// predictable substitutions.
    /// </summary>
    public const int MinimumPasswordLength = 12;

    private User(
        Guid id,
        Email email,
        DisplayName displayName,
        string passwordHash,
        DateTimeOffset createdAt,
        long version,
        DateTimeOffset? emailConfirmedAt)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        Version = version;
        EmailConfirmedAt = emailConfirmedAt;
    }

    /// <summary>The user's identifier.</summary>
    public Guid Id { get; }

    /// <summary>The address they sign in with.</summary>
    public Email Email { get; private set; }

    /// <summary>What they are called in the app.</summary>
    public DisplayName DisplayName { get; private set; }

    /// <summary>
    /// The encoded Argon2id hash. Never logged, returned by an endpoint, or placed on a span.
    /// </summary>
    public string PasswordHash { get; private set; }

    /// <summary>When the account was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// When the owner proved the address is theirs by opening a link mailed to it, or null while they have not.
    /// </summary>
    /// <remarks>
    /// Also set at registration when the instance sends no mail (nothing could confirm it then, and turning mail on
    /// later must not lock those people out), and for the first account.
    /// </remarks>
    public DateTimeOffset? EmailConfirmedAt { get; private set; }

    /// <summary>Whether the address has been confirmed.</summary>
    public bool HasConfirmedEmail => EmailConfirmedAt is not null;

    /// <summary>Incremented by every write; the ETag is derived from it.</summary>
    public long Version { get; private set; }

    /// <summary>Creates a new account.</summary>
    public static User Register(
        Email email,
        DisplayName displayName,
        string passwordHash,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User(CulinaId.New(), email, displayName, passwordHash, createdAt, version: 1, emailConfirmedAt: null);
    }

    /// <summary>
    /// Rebuilds a user from storage, which may restore a state <see cref="Register"/> would not
    /// create.
    /// </summary>
    public static User Restore(
        Guid id,
        Email email,
        DisplayName displayName,
        string passwordHash,
        DateTimeOffset createdAt,
        long version,
        DateTimeOffset? emailConfirmedAt = null)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(displayName);

        return new User(id, email, displayName, passwordHash, createdAt, version, emailConfirmedAt);
    }

    /// <summary>Checks a plaintext password against the length rule.</summary>
    public static Result EnsureAcceptablePassword(string? password) =>
        password is { Length: >= MinimumPasswordLength }
            ? Result.Success()
            : UserErrors.WeakPassword;

    /// <summary>Renames the user.</summary>
    public Result ChangeDisplayName(DisplayName displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);

        DisplayName = displayName;

        return Result.Success();
    }

    /// <summary>Replaces the stored hash.</summary>
    public void ChangePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
    }

    /// <summary>Records that the address is the owner's; a later confirmation keeps the first time.</summary>
    /// <param name="at">The injected current time.</param>
    public void ConfirmEmail(DateTimeOffset at) => EmailConfirmedAt ??= at;

    /// <summary>Changes the address used to sign in.</summary>
    public Result ChangeEmail(Email email)
    {
        ArgumentNullException.ThrowIfNull(email);

        Email = email;

        return Result.Success();
    }
}
