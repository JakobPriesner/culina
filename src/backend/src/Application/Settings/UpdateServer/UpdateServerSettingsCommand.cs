using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Domain.Shared;

namespace Application.Settings.UpdateServer;

/// <summary>Changes the server settings, applying them with a restart.</summary>
/// <param name="Cookies">The session cookie's attributes.</param>
/// <param name="ForwardedHeaders">Which proxies to trust.</param>
/// <param name="RateLimits">The ceilings on what one client may ask for.</param>
/// <param name="OtlpEndpoint">The collector's address, or null to export nothing.</param>
/// <param name="OtlpProtocol"><c>grpc</c> or <c>http_protobuf</c>.</param>
/// <remarks>
/// The groups arrive as the settings records themselves, because a proposal is
/// exactly that: the values the next start would run with. Only the endpoint
/// is still text, so that an address that is not one is reported rather than
/// quietly becoming "none".
/// </remarks>
public sealed record UpdateServerSettingsCommand(
    CookieSettings Cookies,
    ForwardedHeadersSettings ForwardedHeaders,
    RateLimitSettings RateLimits,
    string? OtlpEndpoint,
    string OtlpProtocol);

internal sealed class UpdateServerSettingsCommandHandler(
    CookieSettings cookies,
    ForwardedHeadersSettings proxies,
    RateLimitSettings limits,
    TelemetrySettings telemetry,
    IServerConfiguration configuration,
    IHostRestart host)
    : ICommandHandler<UpdateServerSettingsCommand, ServerChange>
{
    public async Task<Result<ServerChange>> Handle(
        UpdateServerSettingsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Settings.UpdateServer");

        var result = await Proposal(command).Match(
            proposed => ApplyAsync(proposed, cancellationToken),
            error => Task.FromResult(Result<ServerChange>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    /// <summary>
    /// Every group validated exactly as the next startup will validate it, all
    /// failures reported at once.
    /// </summary>
    private static Result<Dictionary<string, string>> Proposal(UpdateServerSettingsCommand command)
    {
        var raw = command.OtlpEndpoint?.Trim();
        Uri? endpoint = null;
        var readable = string.IsNullOrEmpty(raw) || Uri.TryCreate(raw, UriKind.Absolute, out endpoint);

        var exporter = new TelemetrySettings
        {
            Endpoint = endpoint,
            Protocol = OtlpProtocols.FromWire(command.OtlpProtocol)
        };

        return Result.Combine(
                readable
                    ? Result.Success()
                    : SettingsErrors.Invalid(
                        $"The telemetry endpoint must be an http:// or https:// address, and '{raw}' is not one."),
                ServerSettingsFile.Check(command.Cookies.Validate),
                ServerSettingsFile.Check(command.ForwardedHeaders.Validate),
                ServerSettingsFile.Check(command.RateLimits.Validate),
                ServerSettingsFile.Check(exporter.Validate))
            .Map(() => Values(command.Cookies, command.ForwardedHeaders, command.RateLimits, exporter));
    }

    private async Task<Result<ServerChange>> ApplyAsync(
        Dictionary<string, string> proposed,
        CancellationToken cancellationToken)
    {
        var changes = ServerSettingsFile.Changes(proposed, Values(cookies, proxies, limits, telemetry), configuration);

        return changes.Count == 0
            ? ServerChange.None
            : await ServerSettingsFile.SaveAndRestartAsync(changes, configuration, host, cancellationToken)
                .ConfigureAwait(false);
    }

    private static Dictionary<string, string> Values(
        CookieSettings cookies,
        ForwardedHeadersSettings proxies,
        RateLimitSettings limits,
        TelemetrySettings telemetry) =>
        new(
        [
            .. cookies.ToConfigurationValues(),
            .. proxies.ToConfigurationValues(),
            .. limits.ToConfigurationValues(),
            .. telemetry.ToConfigurationValues()
        ]);
}
