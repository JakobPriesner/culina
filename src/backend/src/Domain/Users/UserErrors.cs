using Domain.Shared;

namespace Domain.Users;

/// <summary>
/// Every failure the users module can return.
/// </summary>
/// <remarks>
/// The codes are part of the API contract: clients branch on them, so adding
/// one is safe and renaming one is a breaking change. Errors are declared here
/// once and never constructed inline at a call site.
/// </remarks>
public static class UserErrors
{
    /// <summary>No user with that id exists, or none the caller may see.</summary>
    public static Error NotFound(Guid userId) => new(
        "users.not_found",
        $"No user with id '{userId}' exists.",
        ErrorType.NotFound);

    /// <summary>The value is not an email address.</summary>
    public static readonly Error InvalidEmail = new(
        "users.invalid_email",
        "That is not an email address.",
        ErrorType.Validation);

    /// <summary>The display name is blank or too long.</summary>
    public static readonly Error InvalidDisplayName = new(
        "users.invalid_display_name",
        "A name is required, and it may be at most 80 characters.",
        ErrorType.Validation);

    /// <summary>Someone already registered with that address.</summary>
    public static readonly Error EmailAlreadyUsed = new(
        "users.email_already_used",
        "That email address is already registered.",
        ErrorType.Conflict);

    /// <summary>The instance is not accepting new accounts.</summary>
    public static readonly Error RegistrationClosed = new(
        "users.registration_closed",
        "This instance is not accepting new accounts.",
        ErrorType.Forbidden);

    /// <summary>The instance has as many accounts as it allows.</summary>
    public static readonly Error MaxUsersReached = new(
        "users.max_users_reached",
        "This instance has reached the number of accounts it allows.",
        ErrorType.Forbidden);

    /// <summary>A preference value is not one this app recognises.</summary>
    public static readonly Error InvalidPreference = new(
        "users.invalid_preference",
        "That is not a value this setting accepts.",
        ErrorType.Validation);

    /// <summary>The theme id is blank or too long.</summary>
    public static readonly Error InvalidTheme = new(
        "users.invalid_theme",
        "That is not a theme this app knows.",
        ErrorType.Validation);

    /// <summary>The password does not meet the minimum requirements.</summary>
    public static readonly Error WeakPassword = new(
        "users.weak_password",
        "A password must be at least 12 characters.",
        ErrorType.Validation);
}
