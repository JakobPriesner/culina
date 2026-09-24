using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Contracts.Settings.GetServer;
using Domain.Shared;
using Response = Contracts.Settings.GetServer.Response;

namespace Application.Settings.GetServer;

/// <summary>Reads the server settings the running process uses.</summary>
/// <param name="RemoteAddress">The address the request's connection came from.</param>
/// <param name="Forwarded">Whether the request said it was forwarded for someone else.</param>
/// <param name="ProxyTrusted">Whether that was believed.</param>
public sealed record GetServerSettingsQuery(string? RemoteAddress, bool Forwarded, bool ProxyTrusted);

/// <remarks>
/// Read from the singletons rather than from the configuration: they are what
/// the process actually runs with, defaults applied, which is what a person
/// deciding whether to change something needs to see.
/// </remarks>
internal sealed class GetServerSettingsQueryHandler(
    CookieSettings cookies,
    ForwardedHeadersSettings proxies,
    RateLimitSettings limits,
    TelemetrySettings telemetry,
    IServerConfiguration configuration)
    : IQueryHandler<GetServerSettingsQuery, Response>
{
    public Task<Result<Response>> Handle(GetServerSettingsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Settings.GetServer");

        IEnumerable<string> keys =
        [
            .. cookies.ToConfigurationValues().Keys,
            .. proxies.ToConfigurationValues().Keys,
            .. limits.ToConfigurationValues().Keys,
            .. telemetry.ToConfigurationValues().Keys
        ];

        return Task.FromResult(tracked.Record(Result<Response>.Success(new Response
        {
            Cookies = new CookiesContract
            {
                Secure = cookies.Secure,
                SessionDays = cookies.SessionDays,
                RenewAfterHours = cookies.RenewAfterHours
            },
            ForwardedHeaders = new ForwardedHeadersContract
            {
                KnownProxies = proxies.KnownProxies,
                KnownNetworks = proxies.KnownNetworks
            },
            RateLimits = new RateLimitsContract
            {
                LoginPerIpPerMinute = limits.LoginPerIpPerMinute,
                LoginPerAccountPerMinute = limits.LoginPerAccountPerMinute,
                RegisterPerIpPerHour = limits.RegisterPerIpPerHour,
                InvitationPerIpPerHour = limits.InvitationPerIpPerHour,
                ImportsPerHour = limits.ImportsPerHour,
                SourceRequestsPerHour = limits.SourceRequestsPerHour,
                SharedRecipesPerIpPerMinute = limits.SharedRecipesPerIpPerMinute,
                AssistantRequestsPerHour = limits.AssistantRequestsPerHour,
                RequestsPerSessionPerMinute = limits.RequestsPerSessionPerMinute
            },
            Telemetry = new TelemetryContract
            {
                OtlpEndpoint = telemetry.Endpoint?.OriginalString,
                OtlpProtocol = OtlpProtocols.ToWire(telemetry.Protocol)
            },
            Connection = new ConnectionContract
            {
                RemoteAddress = query.RemoteAddress,
                Forwarded = query.Forwarded,
                ProxyTrusted = query.ProxyTrusted
            },
            Pinned = ServerSettingsFile.Pinned(keys, configuration),
            Writable = configuration.CanSave()
        })));
    }
}
