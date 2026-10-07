using System.Net;
using Application.Settings.GetServer;
using Microsoft.AspNetCore.HttpOverrides;

namespace Api.Endpoints.Settings.GetServer.V1;

/// <summary>Reads, from the request itself, how it reached the server.</summary>
internal static class GetServerSettingsRequestExtensions
{
    /// <remarks>Read after the forwarded-headers middleware: a trusted proxy's address moves to <c>X-Original-For</c>, otherwise <c>X-Forwarded-For</c> remains.</remarks>
    internal static GetServerSettingsQuery ToQuery(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = context.Request.Headers;
        var trusted = headers.ContainsKey(ForwardedHeadersDefaults.XOriginalForHeaderName);

        return new GetServerSettingsQuery(
            Readable(context.Connection.RemoteIpAddress),
            trusted || headers.ContainsKey(ForwardedHeadersDefaults.XForwardedForHeaderName),
            trusted);
    }

    // A dual-stack listener reports an IPv4 client as <c>::ffff:172.18.0.5</c>; the proxy list compares IPv4, so that is the form to offer.
    private static string? Readable(IPAddress? address) =>
        address is null ? null : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
}
