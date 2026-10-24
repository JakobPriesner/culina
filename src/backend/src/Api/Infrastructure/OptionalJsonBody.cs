namespace Api.Infrastructure;

/// <summary>
/// Says an endpoint reads a JSON body it can also do without, attached as endpoint metadata, for OpenAPI only.
/// </summary>
/// <remarks>
/// Declaring the body the framework's way (a bound parameter or <c>Accepts</c>) makes routing refuse a POST with no
/// content type as 415, optional or not; an endpoint that has always been called with no body reads it by hand and
/// describes it with this instead.
/// </remarks>
/// <param name="type">The contract the body is read as.</param>
internal sealed class OptionalJsonBody(Type type)
{
    internal Type Type { get; } = type;

    /// <summary>The body, or null when the request has none.</summary>
    internal static async Task<TBody?> ReadAsync<TBody>(HttpContext context, CancellationToken cancellationToken)
        where TBody : class
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.ContentLength is 0 || !context.Request.HasJsonContentType())
        {
            return null;
        }

        try
        {
            return await context.Request.ReadFromJsonAsync<TBody>(cancellationToken).ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException failure)
        {
            // Answered as any other unreadable body is (GlobalExceptionHandler).
            throw new BadHttpRequestException("The request body is not valid JSON for this operation.", failure);
        }
    }
}
