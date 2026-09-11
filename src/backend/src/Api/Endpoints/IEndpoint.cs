namespace Api.Endpoints;

/// <summary>
/// One HTTP endpoint.
/// </summary>
/// <remarks>
/// Implementations are registered and mapped explicitly, by name — never by
/// assembly scanning. Scanning is a reflection dependency: it breaks trimming,
/// hides the route list from a plain text search, and turns a forgotten
/// endpoint into a runtime surprise instead of a compile error. The cost is one
/// line per endpoint, and an architecture test asserts none is missing.
/// </remarks>
internal interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
