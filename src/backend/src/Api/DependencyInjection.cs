using Api.Authentication;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;
using Microsoft.AspNetCore.Authentication;

namespace Api;

/// <summary>
/// Registers the presentation layer's own services.
/// </summary>
internal static class DependencyInjection
{
    private static IServiceCollection AddCulinaAuthentication(this IServiceCollection services)
    {
        services
            .AddHttpContextAccessor()
            .AddScoped<IUserContext, HttpUserContext>();

        services
            .AddAuthentication(CulinaClaims.Scheme)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
                CulinaClaims.Scheme,
                configureOptions: null);

        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
            AdminRequirementHandler>();

        return services
            .AddSetupAuthorization()
            .AddAuthorizationBuilder()
            .AddAdminPolicy()
            .Services;
    }

    /// <summary>
    /// The policy guarding server and database settings, and the handler that
    /// lets setup through.
    /// </summary>
    private static IServiceCollection AddSetupAuthorization(this IServiceCollection services) =>
        services
            .AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, SetupRequirementHandler>()
            .AddAuthorizationBuilder()
            .AddAdminOrSetupPolicy()
            .Services;

    /// <summary>Knows when this host started, and can start it again.</summary>
    private static IServiceCollection AddHostRestart(this IServiceCollection services) =>
        services
            .AddSingleton<HostRestart>()
            .AddSingleton<IHostRestart>(provider => provider.GetRequiredService<HostRestart>());

    internal static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            // ProblemDetails is required by the exception-handler middleware;
            // the body itself is written by CustomResults, so the shape stays
            // identical to every other error response.
            .AddProblemDetails()
            .AddExceptionHandler<GlobalExceptionHandler>()
            .AddCulinaRateLimiter()
            .AddRequestLogging()
            .AddCulinaJson()
            .AddCulinaOpenApi()
            .AddCulinaAuthentication()
            .AddHostRestart();
    }

    /// <summary>
    /// The presentation layer of the host that runs before there is a
    /// database: problem documents, request logging and the setup policy. No
    /// authentication, no rate limiter, no OpenAPI document — there are no
    /// sessions to authenticate, and the document is exported from the real
    /// host, which maps the same setup routes.
    /// </summary>
    internal static IServiceCollection AddSetupPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddProblemDetails()
            .AddExceptionHandler<GlobalExceptionHandler>()
            .AddRequestLogging()
            .AddCulinaJson()
            .AddSetupAuthorization()
            .AddHostRestart();
    }
}
