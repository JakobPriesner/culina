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

        return services.AddAuthorizationBuilder().AddAdminPolicy().Services;
    }

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
            .AddCulinaAuthentication();
    }
}
