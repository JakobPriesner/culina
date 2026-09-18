using System.Globalization;
using System.Threading.RateLimiting;
using Api.Infrastructure;
using Application.Abstractions.Settings;
using Application.Telemetry;

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

    internal static IServiceCollection AddCulinaRateLimiter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddRateLimiter(options =>
        {
            var limits = Resolve(services);

            options.AddPolicy(Login, context =>
                PerClient(context, limits.LoginPerIpPerMinute, TimeSpan.FromMinutes(1)));

            options.AddPolicy(Register, context =>
                PerClient(context, limits.RegisterPerIpPerHour, TimeSpan.FromHours(1)));

            options.AddPolicy(Invitation, context =>
                PerClient(context, limits.InvitationPerIpPerHour, TimeSpan.FromHours(1)));

            options.AddPolicy(SharedRecipe, context =>
                PerClient(context, limits.SharedRecipesPerIpPerMinute, TimeSpan.FromMinutes(1)));

            options.AddPolicy(Import, context =>
                PerClient(context, limits.ImportsPerHour, TimeSpan.FromHours(1)));

            options.AddPolicy(Source, context =>
                PerClient(context, limits.SourceRequestsPerHour, TimeSpan.FromHours(1)));

            // A generous ceiling on everything else, so one misbehaving client
            // cannot exhaust the connection pool.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                PerClient(context, limits.RequestsPerSessionPerMinute, TimeSpan.FromMinutes(1)));

            options.OnRejected = async (context, cancellationToken) =>
            {
                CulinaTelemetry.RateLimitRejections.Add(1);

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
    /// so a signed-in user's budget follows them across addresses and an
    /// anonymous caller is limited per address.
    /// </summary>
    private static RateLimitPartition<string> PerClient(HttpContext context, int permit, TimeSpan window)
    {
        // The name, not the constant: it loses its `__Host-` prefix wherever
        // cookies are not marked Secure, and a limiter keyed on a cookie that
        // is never there is a limiter that only ever sees an address.
        var cookies = context.RequestServices.GetRequiredService<CookieSettings>();

        var key = context.Request.Cookies.TryGetValue(Authentication.SessionCookies.Name(cookies), out var session)
            ? $"session:{session}"
            : $"ip:{context.Connection.RemoteIpAddress}";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permit,
            Window = window,
            QueueLimit = 0
        });
    }

    private static RateLimitSettings Resolve(IServiceCollection services) =>
        (RateLimitSettings?)services
            .FirstOrDefault(service => service.ServiceType == typeof(RateLimitSettings))
            ?.ImplementationInstance
        ?? throw new InvalidOperationException(
            "AddCulinaRateLimiter must run after AddInfrastructure, which registers RateLimitSettings.");
}
