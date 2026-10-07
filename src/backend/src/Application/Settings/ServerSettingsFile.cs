using System.Globalization;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Shared;

namespace Application.Settings;

/// <summary>
/// Server settings as the configuration keys and strings the next startup reads back, and the one
/// way a change to them is saved.
/// </summary>
/// <remarks>
/// A record goes in and strings come out in the form the settings extensions parse, so comparing a
/// proposal with what is running compares strings produced the same way: a form saved without an
/// edit is no change and no restart.
/// </remarks>
internal static class ServerSettingsFile
{
    /// <summary>The cookie group, by key.</summary>
    internal static Dictionary<string, string> ToConfigurationValues(this CookieSettings cookies) => new()
    {
        [Key(CookieSettings.SectionName, nameof(CookieSettings.Secure))] = Text(cookies.Secure),
        [Key(CookieSettings.SectionName, nameof(CookieSettings.SessionDays))] = Text(cookies.SessionDays),
        [Key(CookieSettings.SectionName, nameof(CookieSettings.RenewAfterHours))] = Text(cookies.RenewAfterHours),
        [Key(CookieSettings.SectionName, nameof(CookieSettings.MaxSessionDays))] = Text(cookies.MaxSessionDays)
    };

    /// <summary>The proxy group, by key.</summary>
    internal static Dictionary<string, string> ToConfigurationValues(this ForwardedHeadersSettings proxies) => new()
    {
        [Key(ForwardedHeadersSettings.SectionName, nameof(ForwardedHeadersSettings.KnownProxies))] =
            string.Join(',', proxies.KnownProxies),
        [Key(ForwardedHeadersSettings.SectionName, nameof(ForwardedHeadersSettings.KnownNetworks))] =
            string.Join(',', proxies.KnownNetworks)
    };

    /// <summary>The rate limits, by key.</summary>
    internal static Dictionary<string, string> ToConfigurationValues(this RateLimitSettings limits) => new()
    {
        [Limit(nameof(RateLimitSettings.LoginPerIpPerMinute))] = Text(limits.LoginPerIpPerMinute),
        [Limit(nameof(RateLimitSettings.LoginPerAccountPerMinute))] = Text(limits.LoginPerAccountPerMinute),
        [Limit(nameof(RateLimitSettings.RegisterPerIpPerHour))] = Text(limits.RegisterPerIpPerHour),
        [Limit(nameof(RateLimitSettings.InvitationPerIpPerHour))] = Text(limits.InvitationPerIpPerHour),
        [Limit(nameof(RateLimitSettings.ImportsPerHour))] = Text(limits.ImportsPerHour),
        [Limit(nameof(RateLimitSettings.SourceRequestsPerHour))] = Text(limits.SourceRequestsPerHour),
        [Limit(nameof(RateLimitSettings.SharedRecipesPerIpPerMinute))] = Text(limits.SharedRecipesPerIpPerMinute),
        [Limit(nameof(RateLimitSettings.AssistantRequestsPerHour))] = Text(limits.AssistantRequestsPerHour),
        [Limit(nameof(RateLimitSettings.ArchiveExportsPerHour))] = Text(limits.ArchiveExportsPerHour),
        [Limit(nameof(RateLimitSettings.RequestsPerSessionPerMinute))] = Text(limits.RequestsPerSessionPerMinute)
    };

    /// <summary>
    /// The exporter's two keys; no endpoint is written as empty, not omitted, so it overrides one
    /// set further down.
    /// </summary>
    internal static Dictionary<string, string> ToConfigurationValues(this TelemetrySettings telemetry) => new()
    {
        [TelemetrySettings.EndpointKey] = telemetry.Endpoint?.OriginalString ?? string.Empty,
        [TelemetrySettings.ProtocolKey] = telemetry.Protocol
    };

    /// <summary>The database group, by key, password included.</summary>
    internal static Dictionary<string, string> ToConfigurationValues(this DatabaseSettings database) => new()
    {
        [Database(nameof(DatabaseSettings.Host))] = database.Host,
        [Database(nameof(DatabaseSettings.Port))] = Text(database.Port),
        [Database(nameof(DatabaseSettings.Name))] = database.Name,
        [Database(nameof(DatabaseSettings.Username))] = database.Username,
        [Database(nameof(DatabaseSettings.Password))] = database.Password,
        [Database(nameof(DatabaseSettings.RequireSsl))] = Text(database.RequireSsl),
        [Database(nameof(DatabaseSettings.MaxPoolSize))] = Text(database.MaxPoolSize)
    };

    /// <summary>
    /// The values that would change what the server runs with: different from now, and not fixed by
    /// the deployment.
    /// </summary>
    internal static Dictionary<string, string> Changes(
        IReadOnlyDictionary<string, string> proposed,
        IReadOnlyDictionary<string, string> current,
        IServerConfiguration configuration) =>
        proposed
            .Where(entry => !configuration.IsPinned(entry.Key))
            .Where(entry => !string.Equals(entry.Value, current.GetValueOrDefault(entry.Key), StringComparison.Ordinal))
            .ToDictionary();

    /// <summary>The keys the deployment fixes, named as the variables that fix them.</summary>
    internal static IReadOnlyList<string> Pinned(IEnumerable<string> keys, IServerConfiguration configuration) =>
        [.. keys.Where(configuration.IsPinned).Select(SettingsKey.Variable)];

    /// <summary>
    /// Saves the changes and restarts to apply them, in that order, so a failed save leaves the
    /// server running on what it had.
    /// </summary>
    internal static async Task<Result<ServerChange>> SaveAndRestartAsync(
        IReadOnlyDictionary<string, string> changes,
        IServerConfiguration configuration,
        IHostRestart host,
        CancellationToken cancellationToken)
    {
        var saved = await configuration.SaveAsync(changes, cancellationToken).ConfigureAwait(false);

        return saved.Map(() =>
        {
            host.Schedule();

            return ServerChange.Restarting;
        });
    }

    /// <summary>
    /// Runs a record's startup validation and reports what it would have refused to start with.
    /// </summary>
    /// <remarks>
    /// The one place an exception becomes a result, deliberately: asking the startup rules again
    /// guarantees the next start accepts what was saved; a second rule set for this screen would
    /// sooner or later allow a value startup refuses.
    /// </remarks>
    internal static Result Check(Action validate)
    {
        try
        {
            validate();

            return Result.Success();
        }
        catch (InvalidOperationException invalid)
        {
            return SettingsErrors.Invalid(invalid.Message);
        }
    }

    private static string Key(string section, string key) => SettingsKey.Of(section, key);

    private static string Limit(string key) => SettingsKey.Of(RateLimitSettings.SectionName, key);

    private static string Database(string key) => SettingsKey.Of(DatabaseSettings.SectionName, key);

    private static string Text(bool value) => value ? "true" : "false";

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
