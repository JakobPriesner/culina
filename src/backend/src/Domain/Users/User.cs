using Domain.Shared;

namespace Domain.Users;

/// <summary>
/// A person with an account.
/// </summary>
/// <remarks>
/// State changes go through methods that return a <see cref="Result"/>, so an
/// invariant cannot be broken by assigning a property.
/// </remarks>
public sealed class User
{
    /// <summary>The shortest password the app accepts.</summary>
    /// <remarks>
    /// Length is the only rule. Composition rules ("one digit, one symbol")
    /// push people toward predictable substitutions and away from passphrases,
    /// which are both longer and easier to remember.
    /// </remarks>
    public const int MinimumPasswordLength = 12;

    private User(Guid id, Email email, DisplayName displayName, string passwordHash, DateTimeOffset createdAt, long version)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        Version = version;
    }

    /// <summary>The user's identifier.</summary>
    public Guid Id { get; }

    /// <summary>The address they sign in with.</summary>
    public Email Email { get; private set; }

    /// <summary>What they are called in the app.</summary>
    public DisplayName DisplayName { get; private set; }

    /// <summary>
    /// The encoded Argon2id hash. Never logged, never returned by an endpoint,
    /// never placed on a span.
    /// </summary>
    public string PasswordHash { get; private set; }

    /// <summary>When the account was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Incremented by every write; the ETag is derived from it.</summary>
    public long Version { get; private set; }

    /// <summary>Creates a new account.</summary>
    /// <param name="email">The validated address.</param>
    /// <param name="displayName">The validated name.</param>
    /// <param name="passwordHash">An already-hashed password.</param>
    /// <param name="createdAt">The injected current time.</param>
    public static User Register(
        Email email,
        DisplayName displayName,
        string passwordHash,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User(CulinaId.New(), email, displayName, passwordHash, createdAt, version: 1);
    }

    /// <summary>Rebuilds a user from storage.</summary>
    /// <remarks>
    /// Separate from <see cref="Register"/> so persistence can restore a state
    /// the domain would not create afresh, without loosening the constructor.
    /// </remarks>
    public static User Restore(
        Guid id,
        Email email,
        DisplayName displayName,
        string passwordHash,
        DateTimeOffset createdAt,
        long version)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(displayName);

        return new User(id, email, displayName, passwordHash, createdAt, version);
    }

    /// <summary>Checks a plaintext password against the length rule.</summary>
    /// <param name="password">The password a person chose.</param>
    public static Result EnsureAcceptablePassword(string? password) =>
        password is { Length: >= MinimumPasswordLength }
            ? Result.Success()
            : UserErrors.WeakPassword;

    /// <summary>Renames the user.</summary>
    /// <param name="displayName">The new name.</param>
    public Result ChangeDisplayName(DisplayName displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);

        DisplayName = displayName;

        return Result.Success();
    }

    /// <summary>Replaces the stored hash.</summary>
    /// <param name="passwordHash">A newly computed hash.</param>
    public void ChangePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
    }

    /// <summary>Changes the address used to sign in.</summary>
    /// <param name="email">The new validated address.</param>
    public Result ChangeEmail(Email email)
    {
        ArgumentNullException.ThrowIfNull(email);

        Email = email;

        return Result.Success();
    }
}
