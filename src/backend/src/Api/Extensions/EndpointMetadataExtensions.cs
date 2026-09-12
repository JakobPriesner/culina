using Api.Infrastructure;

namespace Api.Extensions;

/// <summary>
/// Endpoint metadata the request pipeline reads.
/// </summary>
internal static class EndpointMetadataExtensions
{
    /// <summary>
    /// Declares the query parameters this endpoint accepts. Anything else is
    /// rejected with a 400 by <c>QueryParameterGuardMiddleware</c>, because a
    /// silently ignored filter returns wrong data that looks right.
    /// </summary>
    /// <param name="builder">The endpoint being configured.</param>
    /// <param name="names">Parameters that may appear at most once.</param>
    internal static TBuilder WithQueryParameters<TBuilder>(this TBuilder builder, params string[] names)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithRepeatableQueryParameters(names, []);
    }

    /// <summary>
    /// Declares the query parameters this endpoint accepts, distinguishing the
    /// ones where repeating means something (<c>?tag=vegan&amp;tag=quick</c>).
    /// </summary>
    /// <param name="builder">The endpoint being configured.</param>
    /// <param name="single">Parameters that may appear at most once.</param>
    /// <param name="repeatable">Parameters that may appear several times.</param>
    /// <param name="integers">Which of them are whole numbers, for the contract.</param>
    internal static TBuilder WithRepeatableQueryParameters<TBuilder>(
        this TBuilder builder,
        IReadOnlyCollection<string> single,
        IReadOnlyCollection<string> repeatable,
        IReadOnlyCollection<string>? integers = null)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithMetadata(new AllowedQueryParameters(single, repeatable, integers));

        return builder;
    }
}
