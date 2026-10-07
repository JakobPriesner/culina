using System.Globalization;
using System.Threading.RateLimiting;
using Api.Infrastructure;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Extensions;

/// <summary>
/// The rate limit policies, and the rejection they share.
/// </summary>
/// <remarks>
/// The limiter runs before authentication, so brute force costs nothing to
/// reject: an attacker's request is refused before a password hash is computed
/// or a database connection is taken.
/// </remarks>
internal static class RateLimitExtensions
{
    internal const string Login = "auth-login";
    internal const string Register = "auth-register";
    internal const string Invitation = "auth-invitation";

    /// <summary>
    /// Reading a recipe someone published behind a link.
    /// </summary>
    /// <remarks>
    /// The only anonymous read of a household's content, so it gets a ceiling
    /// of its own rather than sharing the global one with signed-in traffic.
    /// </remarks>
    internal const string SharedRecipe = "shared-recipe";

    /// <summary>
    /// Importing, which is the one thing that makes the server fetch.
    /// </summary>
    /// <remarks>
    /// Limited hard and separately from everything else. Even with every
    /// address checked, a person who can ask the server to open connections
    /// quickly can use it to make a great many of them.
    /// </remarks>
    internal const string Import = "recipe-import";

    /// <summary>
    /// Reading and importing from a library this household has connected.
    /// </summary>
    /// <remarks>
    /// Its own policy rather than <see cref="Import"/>, because the thing being
    /// guarded against is different. That one stops an account aiming this
    /// server at addresses it chooses; this is one fixed address a member set
    /// up with a credential. Sharing the tighter limit made the advertised
    /// feature impossible — eighty batches to move two thousand recipes does
    /// not fit in thirty requests an hour — and a ceiling that forbids the
    /// feature is not a safety measure.
    /// </remarks>
    internal const string Source = "recipe-source";

    /// <summary>
    /// Asking the assistant for anything.
    /// </summary>
    /// <remarks>
    /// The only endpoint in Culina where one request costs real money, so it is
    /// the only one where a ceiling is about the bill rather than about the
    /// server. The budget in settings is the backstop; this is what stops
    /// somebody reaching it in a minute by holding down a button.
    /// </remarks>
    internal const string Assistance = "assistance";

    /// <summary>
    /// The web app reporting what went wrong in it.
    /// </summary>
    /// <remarks>
    /// Anonymous, because a sign-in page can break too, and every request
    /// becomes up to ten log lines — so without a ceiling of its own, anybody
    /// could fill the operator's disk through it — per address, and for every
    /// address together (see <see cref="SharedByEveryone"/>). Fixed rather than a setting:
    /// the app sends at most one batch every few seconds, so this is a limit on
    /// misuse, never on the app, and nobody has a reason to tune it.
    /// </remarks>
    internal const string LogRecords = "log-records";

    private const int LogRecordBatchesPerMinute = 20;

    /// <summary>
    /// What every caller together may send to <see cref="LogRecords"/> in a
    /// minute.
    /// </summary>
    /// <remarks>
    /// A limit per address means little to many addresses, or behind a proxy
    /// trusted too widely, and the endpoint is anonymous. Ten browsers each
    /// reporting as fast as the app ever sends fit; past that, reports are
    /// dropped rather than the operator's disk filled.
    /// </remarks>
    private const int LogRecordBatchesPerMinuteFromEveryone = 120;

    internal static IServiceCollection AddCulinaRateLimiter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddRateLimiter(options =>
        {
            var limits = Resolve(services);

            options.AddPolicy(Login, context =>
                PerAddress(context, limits.LoginPerIpPerMinute, TimeSpan.FromMinutes(1)));

            options.AddPolicy(Register, context =>
                PerAddress(context, limits.RegisterPerIpPerHour, TimeSpan.FromHours(1)));

            options.AddPolicy(Invitation, context =>
                PerAddress(context, limits.InvitationPerIpPerHour, TimeSpan.FromHours(1)));

            options.AddPolicy(SharedRecipe, context =>
                PerAddress(context, limits.SharedRecipesPerIpPerMinute, TimeSpan.FromMinutes(1)));

            options.AddPolicy(Import, context =>
                PerClient(context, limits.ImportsPerHour, TimeSpan.FromHours(1)));

            options.AddPolicy(Source, context =>
                PerClient(context, limits.SourceRequestsPerHour, TimeSpan.FromHours(1)));

            options.AddPolicy(Assistance, context =>
                PerClient(context, limits.AssistantRequestsPerHour, TimeSpan.FromHours(1)));

            options.AddPolicy(LogRecords, context =>
                PerAddress(context, LogRecordBatchesPerMinute, TimeSpan.FromMinutes(1)));

            // A generous ceiling on everything else, so one misbehaving client
            // cannot exhaust the connection pool. Per address, so a made-up
            // session cookie cannot buy a fresh budget. Then the ceilings every
            // caller of one endpoint shares, which an endpoint's own policy
            // cannot add: it has one partition, and that is per address.
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    PerAddress(context, limits.RequestsPerSessionPerMinute, TimeSpan.FromMinutes(1))),
                PartitionedRateLimiter.Create<HttpContext, string>(SharedByEveryone));

            options.OnRejected = async (context, cancellationToken) =>
            {
                CulinaTelemetry.RateLimitRejections.Add(1);
                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(RateLimitExtensions))
                    .Rejected(context.HttpContext.Request, RequestErrors.RateLimited.Code);

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await CustomResults
                    .WriteProblemAsync(context.HttpContext, RequestErrors.RateLimited)
                    .ConfigureAwait(false);

                _ = cancellationToken;
            };
        });
    }

    /// <summary>
    /// Partitions by session when there is one and by client address otherwise,
    /// so a signed-in user's budget follows them across addresses.
    /// </summary>
    /// <remarks>
    /// Only for endpoints that require a session. The limiter runs before the
    /// cookie is checked, so a made-up cookie gets a budget of its own — which
    /// buys nothing here but a 401, and the per-address global limiter bounds
    /// how many of those can be asked for.
    /// </remarks>
    private static RateLimitPartition<string> PerClient(HttpContext context, int permit, TimeSpan window)
    {
        // The name, not the constant: it loses its `__Host-` prefix wherever
        // cookies are not marked Secure, and a limiter keyed on a cookie that
        // is never there is a limiter that only ever sees an address.
        var cookies = context.RequestServices.GetRequiredService<CookieSettings>();

        return context.Request.Cookies.TryGetValue(Authentication.SessionCookies.Name(cookies), out var session)
            ? FixedWindow($"session:{session}", permit, window)
            : PerAddress(context, permit, window);
    }

    /// <summary>
    /// Partitions by client address alone, whatever cookie the request carries.
    /// </summary>
    /// <remarks>
    /// For everything anonymous: an unchecked cookie is whatever the caller
    /// chose to send, and keying on it would give a script a fresh budget with
    /// every request.
    /// </remarks>
    private static RateLimitPartition<string> PerAddress(HttpContext context, int permit, TimeSpan window) =>
        FixedWindow($"ip:{context.Connection.RemoteIpAddress}", permit, window);

    /// <summary>
    /// One budget for every caller of an endpoint that needs one, whoever and
    /// wherever they are.
    /// </summary>
    private static RateLimitPartition<string> SharedByEveryone(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == LogRecords
            ? FixedWindow($"everyone:{LogRecords}", LogRecordBatchesPerMinuteFromEveryone, TimeSpan.FromMinutes(1))
            : RateLimitPartition.GetNoLimiter(string.Empty);

    private static RateLimitPartition<string> FixedWindow(string key, int permit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permit,
            Window = window,
            QueueLimit = 0
        });

    private static RateLimitSettings Resolve(IServiceCollection services) =>
        (RateLimitSettings?)services
            .FirstOrDefault(service => service.ServiceType == typeof(RateLimitSettings))
            ?.ImplementationInstance
        ?? throw new InvalidOperationException(
            "AddCulinaRateLimiter must run after AddInfrastructure, which registers RateLimitSettings.");
}
