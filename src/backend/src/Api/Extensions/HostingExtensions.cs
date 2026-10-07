using System.Net;
using Api.Infrastructure;
using Application.Abstractions.Settings;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace Api.Extensions;

/// <summary>Host-level wiring: forwarded headers, request logging and serving the single-page app.</summary>
internal static class HostingExtensions
{
    /// <summary>One combined line per API request. Bodies are never logged (recipes are personal, logins are credentials).</summary>
    internal static IServiceCollection AddRequestLogging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpLoggingInterceptor<RequestLogInterceptor>();

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

    /// <summary>Serves the built single-page app from the API's origin.</summary>
    /// <remarks>Hashed assets are immutable for a year, other static files short-lived, and the shell <c>no-cache</c> so deploys reach everyone.</remarks>
    internal static WebApplication UseSinglePageApp(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // The shell carries a per-response CSP nonce, so it is rendered, never served as a static file.
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals("/index.html", StringComparison.OrdinalIgnoreCase))
            {
                context.Request.Path = "/";
            }

            await next(context).ConfigureAwait(false);
        });

        // Precompressed copies for clients that can read them. See PrecompressedAssets.
        var webRoot = app.Environment.WebRootFileProvider;

        app.Use(async (context, next) =>
        {
            Infrastructure.PrecompressedAssets.Choose(context, webRoot);

            await next(context).ConfigureAwait(false);
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = Infrastructure.PrecompressedAssets.ContentTypes,
            OnPrepareResponse = context =>
            {
                var headers = context.Context.Response.GetTypedHeaders();

                if (Infrastructure.PrecompressedAssets.Encoding(context.File.Name) is { } coding)
                {
                    context.Context.Response.Headers.ContentEncoding = coding;
                }

                // Decided from the requested file, not the copy on its way out.
                var asked = Infrastructure.PrecompressedAssets.Unencoded(context.Context.Request.Path);

                // The service worker decides how every later request is answered, so it is revalidated every time.
                if (IsServiceWorker(asked))
                {
                    headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

                    return;
                }

                headers.CacheControl = IsImmutable(context.File.Name, asked)
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
    /// <remarks>Rendered and <c>no-store</c> so the inline script can carry this response's CSP nonce; a cached copy would stop running.</remarks>
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

    /// <summary>Answers every API route the setup host does not serve with a <c>503</c> rather than a <c>404</c>, so clients learn setup is the reason.</summary>
    internal static WebApplication MapSetupRequired(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Map($"{Infrastructure.ApiPaths.Prefix}/{{**path}}", () =>
                Infrastructure.CustomResults.Problem(Domain.Shared.SettingsErrors.SetupRequired))
            .ExcludeFromDescription()
            .AllowAnonymous();

        return app;
    }

    private static bool IsServiceWorker(PathString path) =>
        path.Equals("/service-worker.js", StringComparison.OrdinalIgnoreCase);

    private static bool IsImmutable(string fileName, PathString path) =>
        // SvelteKit puts content-hashed assets under /_app/immutable/, the only reliable immutability signal.
        path.StartsWithSegments("/_app/immutable")
        || fileName.Contains(".immutable.", StringComparison.Ordinal);

    /// <summary>Trusts <c>X-Forwarded-*</c> from the configured proxies only, replacing the framework's loopback default.</summary>
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

        foreach (var network in settings.KnownNetworks)
        {
            // Fully qualified: Microsoft.AspNetCore.HttpOverrides has a type of the same name.
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        }

        return (WebApplication)app.UseForwardedHeaders(options);
    }
}
