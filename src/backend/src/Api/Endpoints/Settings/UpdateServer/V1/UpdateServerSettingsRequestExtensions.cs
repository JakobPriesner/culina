using Application.Abstractions.Settings;
using Application.Settings.UpdateServer;
using Request = Contracts.Settings.UpdateServer.Request;

namespace Api.Endpoints.Settings.UpdateServer.V1;

/// <summary>Turns the request into the command it stands for.</summary>
internal static class UpdateServerSettingsRequestExtensions
{
    internal static UpdateServerSettingsCommand ToCommand(this Request request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdateServerSettingsCommand(
            new CookieSettings
            {
                Secure = request.Cookies.Secure,
                SessionDays = request.Cookies.SessionDays,
                RenewAfterHours = request.Cookies.RenewAfterHours
            },
            new ForwardedHeadersSettings
            {
                KnownProxies = Entries(request.ForwardedHeaders.KnownProxies),
                KnownNetworks = Entries(request.ForwardedHeaders.KnownNetworks)
            },
            new RateLimitSettings
            {
                LoginPerIpPerMinute = request.RateLimits.LoginPerIpPerMinute,
                LoginPerAccountPerMinute = request.RateLimits.LoginPerAccountPerMinute,
                RegisterPerIpPerHour = request.RateLimits.RegisterPerIpPerHour,
                InvitationPerIpPerHour = request.RateLimits.InvitationPerIpPerHour,
                ImportsPerHour = request.RateLimits.ImportsPerHour,
                SourceRequestsPerHour = request.RateLimits.SourceRequestsPerHour,
                SharedRecipesPerIpPerMinute = request.RateLimits.SharedRecipesPerIpPerMinute,
                AssistantRequestsPerHour = request.RateLimits.AssistantRequestsPerHour,
                RequestsPerSessionPerMinute = request.RateLimits.RequestsPerSessionPerMinute
            },
            request.Telemetry.OtlpEndpoint,
            request.Telemetry.OtlpProtocol);
    }

    /// <summary>Trimmed, blanks dropped, as the startup reads a comma-separated list.</summary>
    private static IReadOnlyList<string> Entries(IReadOnlyList<string> entries) =>
        [.. entries.Select(entry => entry.Trim()).Where(entry => entry.Length > 0)];
}
