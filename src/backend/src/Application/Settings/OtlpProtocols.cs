using Application.Abstractions.Settings;

namespace Application.Settings;

/// <summary>
/// The exporter's protocol names, and the spelling they travel in.
/// </summary>
/// <remarks>
/// OpenTelemetry writes one of them with a slash; an enum on the wire is
/// lowercase snake_case, so <c>http/protobuf</c> is <c>http_protobuf</c> there.
/// </remarks>
internal static class OtlpProtocols
{
    private const string HttpProtobufOnTheWire = "http_protobuf";

    internal static string ToWire(string protocol) =>
        protocol == TelemetrySettings.HttpProtobuf ? HttpProtobufOnTheWire : protocol;

    internal static string FromWire(string protocol) =>
        protocol == HttpProtobufOnTheWire ? TelemetrySettings.HttpProtobuf : protocol;
}
