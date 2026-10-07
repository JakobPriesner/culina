using System.Globalization;
using System.Threading.RateLimiting;
using Api.Authentication;
using Api.Infrastructure;
using Api.Middleware;
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
/// or a database connection is taken. Only the limits that belong to a person
/// rather than an address are counted later, once it is known who is asking
/// (see <see cref="Personal"/>).
/// </remarks>
internal static class RateLimitExtensions
{
    internal const string Login = "auth-login";
    internal const string Register = "auth-register";

    /// <summary>Redeeming an invitation code.</summary>
    /// <remarks>
    /// Per address rather than per person, unlike the other signed-in limits:
    /// what it stops is somebody guessing codes, and where registration is
    /// open an account costs nothing to make, while an address does.
    /// </remarks>
    internal const string Invitation = "auth-invitation";

    /// <summary>
    /// Reading which household an invitation code is for, before joining.
    /// </summary>
    /// <remarks>
    /// Per address, like redeeming and for the same reason: it answers whether
    /// a code is good, so it is guarded against guessing the way redeeming is,
    /// with the same ceiling. A bucket of its own, though, so opening the join
    /// page never spends a redemption.
    /// </remarks>
    internal const string InvitationLookup = "invitation-lookup";

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
    /// Taking a household's archive, the heaviest read. Restoring one shares the
    /// budget, being the heaviest write.
    /// </summary>
    internal const string Archive = "archive-export";

    /// <summary>
    /// The limits counted per person rather than per address.
    /// </summary>
    /// <remarks>
    /// The limiter middleware runs before authentication, where nobody is known
    /// yet, so there these could only follow the session cookie — and signing
    /// in again bought a fresh budget. Their endpoints all require a session, so
    /// <see cref="PersonalRateLimitMiddleware"/> counts them after authorization
    /// instead, against the user id (see <see cref="PerPerson"/>). The limiter
    /// middleware knows them by name only, and lets them through under the
    /// per-address ceiling every request has.
    /// </remarks>
    private static readonly string[] Personal = [Import, Source, Assistance, Archive];

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

        // The per-person budgets, kept for the life of the host and disposed
        // with it. See PersonalRateLimitMiddleware.
        services.AddSingleton(provider =>
        {
            var limits = provider.GetRequiredService<RateLimitSettings>();

            return PartitionedRateLimiter.Create<HttpContext, string>(context => PerPerson(context, limits));
        });

        return services.AddRateLimiter(options =>
        {
            var limits = Resolve(services);

            options.AddPolicy(Login, context =>
                PerAddress(context, limits.LoginPerIpPerMinute, TimeSpan.FromMinutes(1)));

            options.AddPolicy(Register, context =>
                PerAddress(context, limits.RegisterPerIpPerHour, TimeSpan.FromHours(1)));

            options.AddPolicy(Invitation, context =>
                PerAddress(context, limits.InvitationPerIpPerHour, TimeSpan.FromHours(1)));

            options.AddPolicy(InvitationLookup, context =>
                PerAddress(context, limits.InvitationPerIpPerHour, TimeSpan.FromHours(1)));

            options.AddPolicy(SharedRecipe, context =>
                PerAddress(context, limits.SharedRecipesPerIpPerMinute, TimeSpan.FromMinutes(1)));

            foreach (var personal in Personal)
            {
                options.AddPolicy(personal, _ => RateLimitPartition.GetNoLimiter(string.Empty));
            }

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

            options.OnRejected = (context, _) => new ValueTask(RejectAsync(context.HttpContext, context.Lease));
        });
    }

    /// <summary>
    /// Refuses a request over a limit: counted, logged, told when to try again,
    /// and answered with the problem document every refusal has.
    /// </summary>
    internal static Task RejectAsync(HttpContext context, RateLimitLease lease)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(lease);

        CulinaTelemetry.RateLimitRejections.Add(1);
        context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(RateLimitExtensions))
            .Rejected(context.Request, RequestErrors.RateLimited.Code);

        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        return CustomResults.WriteProblemAsync(context, RequestErrors.RateLimited);
    }

    /// <summary>
    /// The budget a request to a <see cref="Personal"/> endpoint draws on: the
    /// signed-in person's, whichever session or address they use.
    /// </summary>
    /// <remarks>
    /// Asked after authorization, so the user id is always there; the address
    /// is only a floor for a request that somehow arrives without one.
    /// </remarks>
    private static RateLimitPartition<string> PerPerson(HttpContext context, RateLimitSettings limits)
    {
        var policy = context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;

        int? permit = policy switch
        {
            Import => limits.ImportsPerHour,
            Source => limits.SourceRequestsPerHour,
            Assistance => limits.AssistantRequestsPerHour,
            Archive => limits.ArchiveExportsPerHour,
            _ => null
        };

        if (permit is not { } perHour)
        {
            return RateLimitPartition.GetNoLimiter(string.Empty);
        }

        var person = context.User.FindFirst(CulinaClaims.UserId)?.Value
            ?? $"ip:{context.Connection.RemoteIpAddress}";

        return FixedWindow($"{policy}:{person}", perHour, TimeSpan.FromHours(1));
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
