using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Extensions;

/// <summary>
/// The wire format, configured once.
/// </summary>
internal static class JsonExtensions
{
    internal static IServiceCollection AddCulinaJson(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

            // Enums travel as lowercase snake_case strings. An integer enum on
            // the wire cannot be extended safely: inserting a member renumbers
            // everything after it, and an old client then reads a different
            // value than the one that was sent.
            options.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

            // A property the client omits means "unchanged" in a PATCH, so a
            // null must be distinguishable from an absent value rather than
            // being silently dropped on the way out.
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        });
    }
}
