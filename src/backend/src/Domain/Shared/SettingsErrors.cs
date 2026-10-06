namespace Domain.Shared;

/// <summary>Failures when changing instance settings.</summary>
public static class SettingsErrors
{
    /// <summary>A setting was given a value it cannot take.</summary>
    public static readonly Error InvalidValue = new(
        "settings.invalid_value",
        "That is not a value this setting accepts.",
        ErrorType.Validation);

    /// <summary>
    /// The directory server settings are saved to cannot be written.
    /// </summary>
    /// <remarks>
    /// Unavailable rather than a validation failure: nothing about the request
    /// is wrong, and the same request succeeds once the deployment mounts a
    /// volume there.
    /// </remarks>
    public static readonly Error NotWritable = new(
        "settings.not_writable",
        "Culina cannot save server settings, because its configuration directory is not writable. Mount a volume at Storage__ConfigPath (/data/config in the image).",
        ErrorType.Unavailable);

    /// <summary>
    /// Secure cookies turned off on a deployment that has not allowed it.
    /// </summary>
    /// <remarks>
    /// A failure of its own rather than an invalid value, because the screen
    /// offers the switch and has to say why it will not move: the deployment
    /// decides this, not the form.
    /// </remarks>
    public static readonly Error InsecureCookies = new(
        "settings.insecure_cookies",
        "Secure cookies can only be turned off in development, or where the deployment sets Cookies__AllowInsecureOutsideDevelopment=true.",
        ErrorType.Validation);

    /// <summary>Every request but the setup's, until there is a database.</summary>
    public static readonly Error SetupRequired = new(
        "settings.setup_required",
        "Culina is not set up yet. Open it in a browser to connect a database.",
        ErrorType.Unavailable);

    /// <summary>A setting that was not a valid value, in the words of the setting.</summary>
    /// <param name="reason">What is wrong, naming the setting.</param>
    public static Error Invalid(string reason) => new(
        "settings.invalid_value",
        $"That cannot be saved: {reason.TrimEnd('.')}.",
        ErrorType.Validation);

    /// <summary>The database could not be reached with the details given.</summary>
    /// <param name="reason">What the connection attempt reported.</param>
    public static Error DatabaseUnreachable(string reason) => new(
        "settings.database_unreachable",
        $"Culina could not connect to that database: {reason.TrimEnd('.')}.",
        ErrorType.Validation);

    /// <summary>
    /// The database answered, but Culina could not run in it.
    /// </summary>
    /// <param name="reason">What is missing, and the statement that adds it, as a clause.</param>
    /// <remarks>
    /// Found before the settings are saved rather than by the migrations after
    /// the restart, because a migration that fails at startup stops the process
    /// — and a process that stops on every start is an instance nobody can reach
    /// to put the setting back.
    /// </remarks>
    public static Error DatabaseUnsuitable(string reason) => new(
        "settings.database_unsuitable",
        $"Culina cannot run in that database: {reason.TrimEnd('.')}.",
        ErrorType.Validation);
}
