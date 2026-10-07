namespace Domain.Shared;

/// <summary>Failures when changing instance settings.</summary>
public static class SettingsErrors
{
    /// <summary>A setting was given a value it cannot take.</summary>
    public static readonly Error InvalidValue = new(
        "settings.invalid_value",
        "That is not a value this setting accepts.",
        ErrorType.Validation);

    /// <summary>The server settings directory is not writable; unavailable rather than invalid, as the request itself is fine.</summary>
    public static readonly Error NotWritable = new(
        "settings.not_writable",
        "Culina cannot save server settings, because its configuration directory is not writable. Mount a volume at Storage__ConfigPath (/data/config in the image).",
        ErrorType.Unavailable);

    /// <summary>Secure cookies turned off on a deployment that has not allowed it; separate from invalid so the screen can explain.</summary>
    public static readonly Error InsecureCookies = new(
        "settings.insecure_cookies",
        "Secure cookies can only be turned off in development, or where the deployment sets Cookies__AllowInsecureOutsideDevelopment=true.",
        ErrorType.Validation);

    /// <summary>
    /// The settings file is no longer a JSON object. Refused rather than overwritten, so a hand edit is not lost;
    /// unavailable for the same reason as <see cref="NotWritable"/>.
    /// </summary>
    public static readonly Error FileUnreadable = new(
        "settings.file_unreadable",
        "Culina cannot save server settings, because culina.json in its configuration directory is no longer a JSON object it can read. Correct or remove the file, then save again.",
        ErrorType.Unavailable);

    /// <summary>Every request but the setup's, until there is a database.</summary>
    public static readonly Error SetupRequired = new(
        "settings.setup_required",
        "Culina is not set up yet. Open it in a browser to connect a database.",
        ErrorType.Unavailable);

    /// <summary>A setting that was not a valid value.</summary>
    /// <param name="reason">What is wrong, naming the setting.</param>
    public static Error Invalid(string reason) => new(
        "settings.invalid_value",
        $"That cannot be saved: {reason.TrimEnd('.')}.",
        ErrorType.Validation);

    /// <summary>A trusted proxy network so wide that any client inside it could claim any address.</summary>
    /// <param name="network">The network, as entered.</param>
    public static Error ProxyNetworkTooWide(string network) => new(
        "settings.proxy_network_too_wide",
        $"The proxy network '{network}' is too wide to trust: every client in it could claim any address. Name your proxy's own network, at most a /8 for IPv4 or a /32 for IPv6, such as 172.16.0.0/12.",
        ErrorType.Validation);

    // The connection failures below are deliberately coarse: the check connects to any host and port given,
    // so echoing socket or server detail would make it a port scanner. Detail goes to the log.

    /// <summary>Nothing answered at that host and port.</summary>
    public static readonly Error DatabaseUnreachable = new(
        "settings.database_unreachable",
        "Culina could not reach a database server at that address. Check the host and the port, and that the server is running.",
        ErrorType.Validation);

    /// <summary>A PostgreSQL server answered, and refused the role, the password or the database name.</summary>
    public static readonly Error DatabaseLoginRefused = new(
        "settings.database_login_refused",
        "The database server refused the user name, the password or the database name.",
        ErrorType.Validation);

    /// <summary>The connection could not be encrypted, as the settings require.</summary>
    public static readonly Error DatabaseTlsFailed = new(
        "settings.database_tls_failed",
        "Culina could not set up an encrypted connection with that server. Enable TLS on the database server, or turn the TLS requirement off if the database is on the same private network.",
        ErrorType.Validation);

    /// <summary>
    /// The server asked for the password in clear text or as MD5, or for none. Only SCRAM-SHA-256 is used,
    /// so a fake PostgreSQL server cannot harvest the password.
    /// </summary>
    public static readonly Error DatabaseInsecureAuth = new(
        "settings.database_insecure_auth",
        "The database server wants a sign-in method Culina does not use. Culina signs in only with SCRAM-SHA-256: set password_encryption to scram-sha-256, set the role's password again, and use scram-sha-256 in pg_hba.conf.",
        ErrorType.Validation);

    /// <summary>Something answered, but not as a PostgreSQL server Culina could use.</summary>
    public static readonly Error DatabaseNotPostgres = new(
        "settings.database_not_postgres",
        "Something answered at that address, but not a PostgreSQL server Culina can use.",
        ErrorType.Validation);

    /// <summary>
    /// The stored password was to be kept for a different server. "Keep it" holds only for the server it was saved for,
    /// or changing the host would send it elsewhere.
    /// </summary>
    public static readonly Error DatabasePasswordRequired = new(
        "settings.database_password_required",
        "Enter the password again. The host, port, database name or user changed, and the stored password is only ever sent to the server it was saved for.",
        ErrorType.Validation);

    /// <summary>The role is a superuser; refused because it can run programs on the server (<c>COPY … TO PROGRAM</c>).</summary>
    public static readonly Error DatabaseSuperuser = new(
        "settings.database_superuser",
        "Culina must connect as a role of its own, not as a superuser. Create a role that owns the database and enter that instead.",
        ErrorType.Validation);

    /// <summary>
    /// The database answered, but Culina could not run in it. Checked before saving, because a migration failing
    /// at startup would leave an instance nobody can reach to fix the setting.
    /// </summary>
    /// <param name="reason">What is missing, and the statement that adds it, as a clause.</param>
    public static Error DatabaseUnsuitable(string reason) => new(
        "settings.database_unsuitable",
        $"Culina cannot run in that database: {reason.TrimEnd('.')}.",
        ErrorType.Validation);
}
