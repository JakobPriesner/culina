using Application.Abstractions.Settings;

namespace Application.Settings;

/// <summary>
/// The exporter's protocol names and the spelling they travel in: OpenTelemetry writes
/// <c>http/protobuf</c> with a slash, a wire enum is snake_case.
/// </summary>
internal static class OtlpProtocols
{
    private const string HttpProtobufOnTheWire = "http_protobuf";

    internal static string ToWire(string protocol) =>
        protocol == TelemetrySettings.HttpProtobuf ? HttpProtobufOnTheWire : protocol;

    internal static string FromWire(string protocol) =>
        protocol == HttpProtobufOnTheWire ? TelemetrySettings.HttpProtobuf : protocol;
}
