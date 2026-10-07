namespace Api.Endpoints;

/// <summary>One HTTP endpoint.</summary>
/// <remarks>
/// Registered and mapped explicitly by name, never by assembly scanning (it breaks trimming, hides
/// routes from text search and turns a forgotten endpoint into a runtime surprise); an architecture
/// test asserts none is missing.
/// </remarks>
internal interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
