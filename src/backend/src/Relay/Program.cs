using Microsoft.AspNetCore.HttpOverrides;
using Relay;

var builder = WebApplication.CreateSlimBuilder(args);

var options = builder.Configuration.GetSection("Relay").Get<RelayOptions>() ?? new RelayOptions();
var problems = options.Validate();

// Said and exited, not thrown: an unhandled exception at start left the chiseled container spinning a core instead of stopping.
if (problems.Count > 0)
{
    await Console.Error.WriteLineAsync($"The relay is not configured: {string.Join(" ", problems)}").ConfigureAwait(false);

    return 1;
}

// Every relay request is a few hundred bytes of JSON; Kestrel's 30 MB default would only serve an attacker.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 16 * 1024);

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<DeviceStore>();
builder.Services.AddSingleton<Limits>();
builder.Services.ConfigureHttpJsonOptions(json => json.SerializerOptions.TypeInfoResolverChain.Insert(0, RelayJson.Default));

// One long-lived client each: the senders keep their signed tokens, and APNs wants its HTTP/2 connection reused.
if (options.Apns is { } apns)
{
    builder.Services.AddSingleton<IPushSender>(services =>
        new ApnsSender(StoreClient(), apns, services.GetRequiredService<TimeProvider>()));
}

if (options.Fcm is { } fcm)
{
    builder.Services.AddSingleton<IPushSender>(services =>
        new FcmSender(StoreClient(), fcm, services.GetRequiredService<TimeProvider>()));
}

// No request or payload logging: the relay keeps counters, not what anybody was told (7.2).
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

var app = builder.Build();

if (options.BehindProxy)
{
    var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor, ForwardLimit = 1 };
    forwarded.KnownIPNetworks.Clear();
    forwarded.KnownProxies.Clear();
    app.UseForwardedHeaders(forwarded);
}

await app.Services.GetRequiredService<DeviceStore>().InitialiseAsync(CancellationToken.None).ConfigureAwait(false);

app.MapRelay();

await app.RunAsync().ConfigureAwait(false);

return 0;

// Connections are renewed now and then, so a store's DNS change reaches the relay without a restart.
static HttpClient StoreClient() =>
    new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15), EnableMultipleHttp2Connections = true })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };
