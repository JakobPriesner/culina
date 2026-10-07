using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Extensions;

/// <summary>The wire format, configured once.</summary>
internal static class JsonExtensions
{
    internal static IServiceCollection AddCulinaJson(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

            // Enums travel as lowercase snake_case strings: an integer enum cannot be extended
            // safely (inserting renumbers).
            options.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

            // Absent means "unchanged" in a PATCH, so a null must stay distinguishable from an
            // omitted value.
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        });
    }
}
