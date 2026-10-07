using System.Net;
using System.Text.Json;

namespace Infrastructure.Assistance;

/// <summary>
/// The transport rules every provider client keeps: redirects are not followed (they would carry the API key away).
/// Deliberately separate from <c>SourceHttp</c>, which refuses private networks; admin-configured addresses may point at them.
/// </summary>
internal sealed class AssistantHttp : IDisposable
{
    /// <summary>How long one call may take: below the frontend's sixty seconds, so a stalled provider cannot hold a request open.</summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(55);

    /// <summary>How long a drawing may take; image generation is slow, but a silently stalled provider must be noticed.</summary>
    private static readonly TimeSpan DrawingDeadline = TimeSpan.FromMinutes(2);

    private readonly SocketsHttpHandler handler;
    private readonly HttpClient client;
    private readonly HttpClient patient;

    public AssistantHttp()
    {
        handler = new SocketsHttpHandler
        {
            // A followed redirect would hand the API key to wherever it pointed.
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10)
        };

        client = Build(Deadline);
        patient = Build(DrawingDeadline);
    }

    /// <summary>The client itself, for an SDK that carries its own address.</summary>
    /// <param name="drawing">Whether this is the call that makes a picture, which is allowed longer.</param>
    internal HttpClient Client(bool drawing = false) => drawing ? patient : client;

    /// <summary>A client of its own for one provider, over this one's shared handler (a new client over an old handler is cheap).</summary>
    /// <param name="baseUrl">Where that provider lives.</param>
    /// <param name="drawing">Whether this is the call that makes a picture, which is allowed longer.</param>
    internal HttpClient ClientFor(string baseUrl, bool drawing = false) =>
        new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = drawing ? DrawingDeadline : Deadline
        };

    private HttpClient Build(TimeSpan deadline)
    {
        var built = new HttpClient(handler, disposeHandler: false) { Timeout = deadline };

        built.DefaultRequestHeaders.UserAgent.ParseAdd("Culina/1.0 (self-hosted recipe app)");

        return built;
    }

    /// <summary>How the recipe JSON the model wrote inside a provider's answer is read.</summary>
    internal static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public void Dispose()
    {
        client.Dispose();
        patient.Dispose();
        handler.Dispose();
    }
}
