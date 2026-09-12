using System.Net;
using Application.Abstractions.Settings;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace Api.Extensions;

/// <summary>
/// Host-level wiring: forwarded headers, request logging, and serving the
/// single-page app.
/// </summary>
internal static class HostingExtensions
{
    /// <summary>
    /// One combined line per request: method, path, status, duration.
    /// </summary>
    /// <remarks>
    /// Request and response bodies are never logged, in any environment. A
    /// recipe body is personal content and a login body is a credential.
    /// </remarks>
    internal static IServiceCollection AddRequestLogging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddHttpLogging(options =>
        {
            options.LoggingFields =
                HttpLoggingFields.RequestMethod
                | HttpLoggingFields.RequestPath
                | HttpLoggingFields.ResponseStatusCode
                | HttpLoggingFields.Duration;

            options.CombineLogs = true;
        });
    }

    /// <summary>
    /// Serves the built single-page app from the same origin as the API.
    /// </summary>
    /// <remarks>
    /// Hashed build assets are immutable for a year; unhashed static files get
    /// a short lifetime so a fix reaches clients the same day; and the shell
    /// itself is <c>no-cache</c>, because a cached shell means a deploy never
    /// reaches anyone.
    /// </remarks>
    internal static WebApplication UseSinglePageApp(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // The shell is never served as a static file: it carries a per-response
        // CSP nonce, so it has to be rendered. Asking for it by name lands on
        // the same rendered document as asking for the route.
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals("/index.html", StringComparison.OrdinalIgnoreCase))
            {
                context.Request.Path = "/";
            }

            await next(context).ConfigureAwait(false);
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                var headers = context.Context.Response.GetTypedHeaders();

                // The service worker decides what every later request is
                // answered with, so a stale copy of it is a stale copy of the
                // whole app. It is revalidated every time, never reused blind.
                if (IsServiceWorker(context.Context.Request.Path))
                {
                    headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

                    return;
                }

                headers.CacheControl = IsImmutable(context.File.Name, context.Context.Request.Path)
                    ? new CacheControlHeaderValue
                    {
                        Public = true,
                        MaxAge = TimeSpan.FromDays(365),
                        Extensions = { new NameValueHeaderValue("immutable") }
                    }
                    : new CacheControlHeaderValue { Public = true, MaxAge = TimeSpan.FromHours(1) };
            }
        });

        return app;
    }

    /// <summary>Sends any unmatched non-API route to the app shell.</summary>
    /// <remarks>
    /// The shell is rendered rather than sent from disk so the inline
    /// theme-before-paint script can carry this response's CSP nonce, and it is
    /// <c>no-store</c> for the same reason: a cached copy would carry a nonce
    /// the next response's policy does not name, and the script would silently
    /// stop running.
    /// </remarks>
    internal static WebApplication MapSinglePageAppFallback(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var shell = Infrastructure.AppShell.Load(app.Environment.WebRootPath);

        app.MapFallback(async context =>
        {
            if (Infrastructure.ApiPaths.IsApi(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await Infrastructure.StatusCodeProblems.WriteAsync(context).ConfigureAwait(false);

                return;
            }

            if (!shell.Exists)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;

                return;
            }

            context.Response.Headers.CacheControl = "no-store";
            context.Response.ContentType = "text/html; charset=utf-8";

            var nonce = Infrastructure.RequestContext.CspNonce(context) ?? string.Empty;

            await context.Response
                .WriteAsync(shell.Render(nonce), context.RequestAborted)
                .ConfigureAwait(false);
        });

        return app;
    }

    private static bool IsServiceWorker(PathString path) =>
        path.Equals("/service-worker.js", StringComparison.OrdinalIgnoreCase);

    private static bool IsImmutable(string fileName, PathString path) =>
        // SvelteKit puts content-hashed assets under /_app/immutable/, which is
        // the only reliable signal that a file's contents can never change.
        path.StartsWithSegments("/_app/immutable")
        || fileName.Contains(".immutable.", StringComparison.Ordinal);

    /// <summary>
    /// Trusts <c>X-Forwarded-*</c> from the configured proxies only.
    /// </summary>
    /// <remarks>
    /// Trusting every proxy would let any client forge its own address, which
    /// would make per-IP rate limiting useless and every security log line
    /// name the wrong host. The framework default trusts loopback; the
    /// configured list replaces that entirely, so nothing is trusted by
    /// accident.
    /// </remarks>
    internal static WebApplication UseCulinaForwardedHeaders(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var settings = app.Services.GetRequiredService<ForwardedHeadersSettings>();
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var proxy in settings.KnownProxies)
        {
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        }

        return (WebApplication)app.UseForwardedHeaders(options);
    }
}
