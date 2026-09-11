using Microsoft.Extensions.DependencyInjection;

namespace Application;

/// <summary>
/// Registers every command and query handler, explicitly, one line each.
/// </summary>
/// <remarks>
/// <para>
/// No scanning. A handler that is deleted, renamed or never registered must
/// fail the build rather than surface as a 500 on the one route nobody tested;
/// an architecture test asserts that every handler in this assembly appears
/// here.
/// </para>
/// <para>
/// Handlers are scoped: they hold a unit of work and repositories, which belong
/// to one request.
/// </para>
/// </remarks>
public static class DependencyInjection
{
    /// <summary>Adds the use cases.</summary>
    /// <param name="services">The container to register into.</param>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Handlers are grouped by domain, in the same order as the folders.
        // Nothing depends on the order itself.
        return services;
    }
}
