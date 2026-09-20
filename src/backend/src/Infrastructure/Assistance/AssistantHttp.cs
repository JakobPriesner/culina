using System.Net;
using System.Text.Json;

namespace Infrastructure.Assistance;

/// <summary>
/// The rules every provider's client is made to keep.
/// </summary>
/// <remarks>
/// <para>
/// This used to be the one way this app talked to a provider — a request
/// builder, a reader, and a table turning status codes into errors. All three
/// providers now speak through their own client libraries, so what is left is
/// the part that was never theirs to decide: the transport, and the shape of
/// the JSON this app still reads out of an answer.
/// </para>
/// <para>
/// Built on the same reasoning as <see cref="Infrastructure.Import.SourceHttp"/>
/// and deliberately not shared with it: that one guards against an address a
/// user typed and so refuses private networks, while this one is pointed at an
/// address an administrator configured and pointing it at the machine next door
/// is the ordinary reason to configure it at all. Two clients with two threat
/// models beats one with a flag.
/// </para>
/// <para>
/// Redirects are not followed, and that is the whole reason an SDK is handed a
/// client from here rather than left to build its own: a followed redirect
/// carries the API key to wherever it pointed.
/// </para>
/// </remarks>
internal sealed class AssistantHttp : IDisposable
{
    /// <summary>
    /// How long one call may take.
    /// </summary>
    /// <remarks>
    /// Generous, because this is the slow thing: a model writing a whole recipe
    /// is tens of seconds on a bad day, and the frontend already waits sixty
    /// for it. Short enough that a provider which has stopped answering does
    /// not hold a request open until the proxy closes it.
    /// </remarks>
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(55);

    /// <summary>
    /// How long a drawing may take.
    /// </summary>
    /// <remarks>
    /// Drawing is not writing with a picture at the end of it: it is the one
    /// call in this app where a provider spends real time on a machine of its
    /// own, and sixteen seconds is a fast one. Two minutes is long enough for a
    /// slow prompt on a busy afternoon and still short enough that a provider
    /// which has silently stopped answering is noticed the same day.
    /// </remarks>
    private static readonly TimeSpan DrawingDeadline = TimeSpan.FromMinutes(2);

    private readonly SocketsHttpHandler handler;
    private readonly HttpClient client;
    private readonly HttpClient patient;

    public AssistantHttp()
    {
        handler = new SocketsHttpHandler
        {
            // Nothing legitimate redirects an API call, and a followed redirect
            // would hand the key to whatever it pointed at.
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10)
        };

        client = Build(Deadline);
        patient = Build(DrawingDeadline);
    }

    /// <summary>
    /// The client itself, for an SDK that carries its own address.
    /// </summary>
    /// <param name="drawing">
    /// Whether this is the call that makes a picture, which is allowed longer.
    /// </param>
    internal HttpClient Client(bool drawing = false) => drawing ? patient : client;

    /// <summary>
    /// A client of its own for one provider, over this one's connection pool.
    /// </summary>
    /// <param name="baseUrl">Where that provider lives.</param>
    /// <param name="drawing">
    /// Whether this is the call that makes a picture, which is allowed longer.
    /// </param>
    /// <remarks>
    /// For an SDK that wants a client with an address on it. The handler is
    /// shared and not disposed with the wrapper, so this costs an object rather
    /// than a connection pool — the mistake that makes an app run out of
    /// sockets is a new <c>HttpClient</c> with a new handler, not a new
    /// <c>HttpClient</c> over an old one.
    /// </remarks>
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

    /// <summary>
    /// How the answer inside an answer is read.
    /// </summary>
    /// <remarks>
    /// The providers' own libraries parse their own envelopes. What is left for
    /// this app to parse is the recipe the model wrote inside one, which is a
    /// string of JSON either way.
    /// </remarks>
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
