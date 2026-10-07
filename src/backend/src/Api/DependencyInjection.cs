using Api.Authentication;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;
using Microsoft.AspNetCore.Authentication;

namespace Api;

/// <summary>Registers the presentation layer's own services.</summary>
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

    // The policy guarding server and database settings, and the handler that lets setup through.
    private static IServiceCollection AddSetupAuthorization(this IServiceCollection services) =>
        services
            .AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, SetupRequirementHandler>()
            .AddAuthorizationBuilder()
            .AddAdminOrSetupPolicy()
            .Services;

    // Knows when this host started, and can start it again.
    private static IServiceCollection AddHostRestart(this IServiceCollection services) =>
        services
            .AddSingleton<HostRestart>()
            .AddSingleton<IHostRestart>(provider => provider.GetRequiredService<HostRestart>());

    internal static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            // Required by the exception-handler middleware; CustomResults writes the body so the shape matches every other error.
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
    /// The presentation layer of the host that runs before there is a database: no authentication or OpenAPI document,
    /// and a rate limiter whose one limit is the fixed <see cref="DatabaseCheckLimit"/> of the database endpoint.
    /// </summary>
    internal static IServiceCollection AddSetupPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddProblemDetails()
            .AddExceptionHandler<GlobalExceptionHandler>()
            .AddRateLimiter(_ => { })
            .AddRequestLogging()
            .AddCulinaJson()
            .AddSetupAuthorization()
            .AddHostRestart();
    }
}
