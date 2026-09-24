namespace Application.Abstractions.Settings;

/// <summary>
/// Where traces, metrics and logs are exported to, if anywhere.
/// </summary>
/// <remarks>
/// <para>
/// The keys are the standard OpenTelemetry names rather than a Culina section,
/// because the SDK reads them itself: whatever is set here is what the exporter
/// uses, and a collector's documentation can be followed word for word.
/// </para>
/// <para>
/// Unset means JSON logs on stdout and nothing exported — the exporter is not
/// even registered, because without a collector it retries against localhost
/// forever and fills the log with noise.
/// </para>
/// </remarks>
public sealed record TelemetrySettings
{
    /// <summary>
    /// No section: the OpenTelemetry names sit at the root of the configuration.
    /// </summary>
    public const string SectionName = "";

    /// <summary>The collector's address.</summary>
    public const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>How to talk to it.</summary>
    public const string ProtocolKey = "OTEL_EXPORTER_OTLP_PROTOCOL";

    /// <summary>The SDK's default, and what port 4317 speaks.</summary>
    public const string Grpc = "grpc";

    /// <summary>What port 4318 speaks, and most hosted collectors.</summary>
    public const string HttpProtobuf = "http/protobuf";

    /// <summary>The collector's address, or null to export nothing.</summary>
    public Uri? Endpoint { get; init; }

    /// <summary>
    /// <see cref="Grpc"/> or <see cref="HttpProtobuf"/>.
    /// </summary>
    /// <remarks>
    /// Worth having next to the address, because the wrong one fails silently:
    /// a gRPC exporter pointed at an HTTP port exports nothing and says nothing.
    /// </remarks>
    public string Protocol { get; init; } = Grpc;

    /// <summary>Throws when any value would make the process unable to serve.</summary>
    public void Validate()
    {
        if (Endpoint is { } endpoint
            && (!endpoint.IsAbsoluteUri || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps)))
        {
            throw new InvalidOperationException(
                $"Configuration {EndpointKey} must be an http:// or https:// address, but was '{endpoint}'.");
        }

        if (Protocol is not (Grpc or HttpProtobuf))
        {
            throw new InvalidOperationException(
                $"Configuration {ProtocolKey} must be '{Grpc}' or '{HttpProtobuf}', but was '{Protocol}'.");
        }
    }
}
