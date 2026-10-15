using System.Net;
using System.Net.Http.Json;
using Domain.Import;

namespace Infrastructure.Import;

/// <summary>What the push relay said about one notification.</summary>
internal enum RelayOutcome
{
    /// <summary>Handed to Apple or Google.</summary>
    Delivered,

    /// <summary>The relay or the store no longer knows the device: forget it.</summary>
    Gone,

    /// <summary>Not now (limit, outage, a store that would not take it): try again later.</summary>
    Retry
}

/// <summary>Posts notifications to the publisher's push relay (docs/native-app-design.md, 7.1).</summary>
internal sealed class RelayTransport : IDisposable
{
    private readonly HttpClient http;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The owned HttpClient disposes its handler and is disposed by this singleton.")]
    public RelayTransport() : this(new HttpClient(CheckedConnections.PublicHandler(), disposeHandler: true) { Timeout = TimeSpan.FromSeconds(15) }) { }

    internal RelayTransport(HttpClient http) => this.http = http;

    internal async Task<RelayOutcome> SendAsync(Uri relay, RelayNotification notification, CancellationToken token)
    {
        try
        {
            using var response = await http
                .PostAsJsonAsync(new Uri(relay, "v1/notifications"), notification, token)
                .ConfigureAwait(false);

            return response.StatusCode switch
            {
                HttpStatusCode.Accepted or HttpStatusCode.OK => RelayOutcome.Delivered,
                HttpStatusCode.Gone => RelayOutcome.Gone,
                _ => RelayOutcome.Retry
            };
        }
        catch (HttpRequestException)
        {
            return RelayOutcome.Retry;
        }
        catch (TaskCanceledException) when (!token.IsCancellationRequested)
        {
            return RelayOutcome.Retry;
        }
    }

    public void Dispose() => http.Dispose();
}

/// <summary>What crosses the relay: words, a path and a tag, never a recipe, a name or this server's address (7.2).</summary>
internal sealed record RelayNotification(string RelayToken, string Title, string Body, string Path, string Tag);
