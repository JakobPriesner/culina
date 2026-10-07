using System.Globalization;
using System.Threading.RateLimiting;
using Api.Authentication;
using Api.Infrastructure;
using Api.Middleware;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Extensions;

/// <summary>The rate limit policies, and the rejection they share.</summary>
/// <remarks>
/// The limiter runs before authentication, so brute force is refused before any password hash or
/// database connection. Limits that belong to a person rather than an address are counted later
/// (see <see cref="Personal"/>).
/// </remarks>
internal static class RateLimitExtensions
{
    internal const string Login = "auth-login";
    internal const string Register = "auth-register";

    /// <summary>Redeeming an invitation code.</summary>
    /// <remarks>
    /// Per address, not per person: it stops code guessing, and where registration is open an
    /// account costs nothing but an address does.
    /// </remarks>
    internal const string Invitation = "auth-invitation";

    /// <summary>Reading which household an invitation code is for, before joining.</summary>
    /// <remarks>
    /// Per address with the same ceiling as redeeming, since it also answers whether a code is
    /// good; a bucket of its own so opening the join page spends no redemption.
    /// </remarks>
    internal const string InvitationLookup = "invitation-lookup";

    /// <summary>
    /// Reading a recipe someone published behind a link: the only anonymous read of household
    /// content, so it has its own ceiling.
    /// </summary>
    internal const string SharedRecipe = "shared-recipe";

    /// <summary>
    /// Importing, the one thing that makes the server fetch; limited hard and separately because
    /// opening connections quickly is itself abuse.
    /// </summary>
    internal const string Import = "recipe-import";

    /// <summary>Reading and importing from a library this household has connected.</summary>
    /// <remarks>
    /// Its own policy: <see cref="Import"/> stops aiming the server at arbitrary addresses, this is
    /// one fixed address a member set up, and the tighter limit made moving a 2,000-recipe library
    /// impossible.
    /// </remarks>
    internal const string Source = "recipe-source";

    /// <summary>Asking the assistant, the one endpoint where a request costs real money.</summary>
    /// <remarks>
    /// The budget in settings is the backstop; this stops reaching it in a minute by holding a
    /// button.
    /// </remarks>
    internal const string Assistance = "assistance";

    /// <summary>
    /// Taking a household's archive, the heaviest read; restoring one shares the budget, being the
    /// heaviest write.
    /// </summary>
    internal const string Archive = "archive-export";

    /// <summary>The limits counted per person rather than per address.</summary>
    /// <remarks>
    /// The limiter middleware runs before authentication and knows these by name only;
    /// <see cref="PersonalRateLimitMiddleware"/> counts them after authorization against the user
    /// id (see <see cref="PerPerson"/>), so signing in again buys no fresh budget.
    /// </remarks>
    private static readonly string[] Personal = [Import, Source, Assistance, Archive];

    /// <summary>The web app reporting what went wrong in it.</summary>
    /// <remarks>
    /// Anonymous (a sign-in page can break too) and each request becomes up to ten log lines, so it
    /// is limited per address and for everyone together (see <see cref="SharedByEveryone"/>). Fixed
    /// rather than a setting: it limits misuse, never the app.
    /// </remarks>
    internal const string LogRecords = "log-records";

    private const int LogRecordBatchesPerMinute = 20;

    /// <summary>
    /// What every caller together may send to <see cref="LogRecords"/> in a minute.
    /// </summary>
    /// <remarks>
    /// A per-address limit means little against many addresses or an over-trusted proxy; past this,
    /// reports are dropped rather than filling the operator's disk.
    /// </remarks>
    private const int LogRecordBatchesPerMinuteFromEveryone = 120;

    internal static IServiceCollection AddCulinaRateLimiter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Per-person budgets live as long as the host (see PersonalRateLimitMiddleware).
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

            // A generous per-address ceiling so one client cannot exhaust the connection pool
            // (keyed on address, not cookie, so a made-up cookie buys nothing). Then the ceilings
            // every caller of one endpoint shares, which an endpoint's own per-address policy
            // cannot add.
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    PerAddress(context, limits.RequestsPerSessionPerMinute, TimeSpan.FromMinutes(1))),
                PartitionedRateLimiter.Create<HttpContext, string>(SharedByEveryone));

            options.OnRejected = (context, _) => new ValueTask(RejectAsync(context.HttpContext, context.Lease));
        });
    }

    /// <summary>
    /// Refuses a request over a limit: counted, logged, told when to retry, and answered with the
    /// usual problem document.
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
    /// The budget a <see cref="Personal"/> endpoint draws on: the signed-in person's, whichever
    /// session or address they use.
    /// </summary>
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
    /// Partitions by client address alone, whatever cookie the request carries; for everything
    /// anonymous.
    /// </summary>
    private static RateLimitPartition<string> PerAddress(HttpContext context, int permit, TimeSpan window) =>
        FixedWindow($"ip:{context.Connection.RemoteIpAddress}", permit, window);

    /// <summary>
    /// One budget for every caller of an endpoint, whoever and wherever they are.
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
