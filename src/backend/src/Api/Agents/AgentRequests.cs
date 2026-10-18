using System.Text;
using Microsoft.AspNetCore.Http.Features;

namespace Api.Agents;

/// <summary>Sends a tool call's request through the app's own pipeline, in memory, as the agent that asked.</summary>
/// <remarks>
/// The request carries the asking request's bearer token, client address, scheme, host and language, so it is
/// authenticated, rate limited and logged as that agent's, and runs under the same trace. Nothing travels over a
/// socket: a loopback call would count every agent against one local address and hide where it came from.
/// </remarks>
internal sealed class AgentRequests(ApplicationPipeline pipeline, IServiceScopeFactory scopes)
{
    internal async Task<AgentReply> SendAsync(HttpContext asking, AgentRequest request)
    {
        ArgumentNullException.ThrowIfNull(asking);
        ArgumentNullException.ThrowIfNull(request);

        var context = new DefaultHttpContext();
        var response = new RecordedResponse();
        using var body = new MemoryStream();

        context.Features.Set<IHttpResponseFeature>(response);
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection(request.Json is not null));
        context.Response.Body = body;
        context.Connection.RemoteIpAddress = asking.Connection.RemoteIpAddress;
        context.RequestAborted = asking.RequestAborted;
        Describe(context.Request, asking.Request, request);

        var scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            context.RequestServices = scope.ServiceProvider;

            await pipeline.Built(context).ConfigureAwait(false);
            await response.FinishAsync().ConfigureAwait(false);
        }

        var headers = context.Response.Headers;

        return new AgentReply(
            context.Response.StatusCode,
            headers.ETag.Count > 0 ? headers.ETag.ToString() : null,
            headers.Location.Count > 0 ? headers.Location.ToString() : null,
            context.Response.ContentType,
            body.ToArray());
    }

    private static void Describe(HttpRequest target, HttpRequest asking, AgentRequest request)
    {
        target.Method = request.Method;
        target.Protocol = asking.Protocol;
        target.Scheme = asking.Scheme;
        target.Host = asking.Host;
        target.PathBase = asking.PathBase;
        target.Path = new PathString(request.Path);
        target.QueryString = request.Query;
        target.Headers.Authorization = asking.Headers.Authorization;
        target.Headers.AcceptLanguage = asking.Headers.AcceptLanguage;
        target.Headers.Accept = "application/json";

        if (request.IfMatch is { } etag)
        {
            target.Headers.IfMatch = etag;
        }

        if (request.Json is { } json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);

            target.ContentType = "application/json";
            target.ContentLength = bytes.Length;
            target.Body = new MemoryStream(bytes);
        }
    }

    /// <summary>A response that runs its starting and completion callbacks once the pipeline is done, as a server would.</summary>
    /// <remarks>
    /// The headers set on starting (request id, security headers) are part of what the pipeline produces, and a
    /// completion callback may release something the request held.
    /// </remarks>
    private sealed class RecordedResponse : HttpResponseFeature
    {
        private readonly Stack<(Func<object, Task> Callback, object State)> starting = new();
        private readonly Stack<(Func<object, Task> Callback, object State)> completed = new();
        private bool started;

        public override bool HasStarted => started;

        public override void OnStarting(Func<object, Task> callback, object state) => starting.Push((callback, state));

        public override void OnCompleted(Func<object, Task> callback, object state) => completed.Push((callback, state));

        // A stack enumerates the last registered first, the order a server runs them in.
        internal async Task FinishAsync()
        {
            foreach (var (callback, state) in starting)
            {
                await callback(state).ConfigureAwait(false);
            }

            started = true;

            foreach (var (callback, state) in completed)
            {
                await callback(state).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Tells body binding whether there is a body to read, which a server works out from the headers.</summary>
    private sealed class BodyDetection(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => canHaveBody;
    }
}
