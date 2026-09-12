using System.Text.Json;
using System.Text.Json.Nodes;
using Api.Infrastructure;
using Domain.Recipes;
using Microsoft.OpenApi;

namespace Api.Extensions;

/// <summary>
/// Corrects two places where the generated document describes the framework
/// rather than the API.
/// </summary>
/// <remarks>
/// The document is the contract the frontend client is generated from, so a
/// wrong schema here becomes a wrong type in every component that touches it.
/// Both corrections are made once, centrally, instead of being cast away on
/// the client.
/// </remarks>
internal static class OpenApiContractFixes
{
    /// <summary>
    /// Restores integers to being integers, everywhere in the document.
    /// </summary>
    /// <remarks>
    /// The schema exporter describes every whole number as "an integer or a
    /// string of digits", because a JSON number cannot carry the full range of
    /// an Int64. Culina never writes a number as a string and never reads one,
    /// so leaving it would give the client a <c>number | string</c> for every
    /// count, minute and version — and a comparison against a version would
    /// silently become a string comparison.
    ///
    /// Done on the finished document rather than per schema: the union is a
    /// type-flag set on the model, and a schema transformer's correction to it
    /// does not survive the document being assembled.
    /// </remarks>
    internal static void CollapseStringOrIntegerUnions(OpenApiDocument document)
    {
        var seen = new HashSet<IOpenApiSchema>();

        foreach (var schema in document.Components?.Schemas?.Values ?? [])
        {
            Visit(schema, seen);
        }
    }

    private static void Visit(IOpenApiSchema? schema, HashSet<IOpenApiSchema> seen)
    {
        // Schemas reference each other, and a recipe that contains a recipe
        // would otherwise recurse forever.
        if (schema is null || !seen.Add(schema))
        {
            return;
        }

        if (schema is OpenApiSchema concrete)
        {
            Collapse(concrete);
        }

        foreach (var child in Children(schema))
        {
            Visit(child, seen);
        }
    }

    private static IEnumerable<IOpenApiSchema?> Children(IOpenApiSchema schema) =>
    [
        schema.Items,
        schema.AdditionalProperties,
        .. schema.Properties?.Values ?? [],
        .. schema.AnyOf ?? [],
        .. schema.AllOf ?? [],
        .. schema.OneOf ?? []
    ];

    private static void Collapse(OpenApiSchema schema)
    {
        if (schema.Type is not { } types)
        {
            return;
        }

        var numeric = types & (JsonSchemaType.Integer | JsonSchemaType.Number);

        if (numeric == default || (types & JsonSchemaType.String) == default)
        {
            return;
        }

        // A null in the set is how an optional value is spelled, so it stays.
        schema.Type = types & ~JsonSchemaType.String;

        // The pattern only existed to validate the string half.
        schema.Pattern = null;
    }

    /// <summary>
    /// Publishes the closed vocabularies the contract carries as plain strings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Contracts is a leaf: it cannot reference the domain, so a unit travels
    /// as a string rather than as <see cref="Unit"/>. Without this the document
    /// says "some string" and the generated client has to redeclare the
    /// vocabulary — which is the one thing sharing a contract is supposed to
    /// prevent.
    /// </para>
    /// <para>
    /// Listed explicitly rather than matched by property name. A rule that
    /// attaches an enum to anything called <c>unit</c> would eventually attach
    /// it to something that is not one.
    /// </para>
    /// </remarks>
    internal static void PublishVocabularies(OpenApiDocument document)
    {
        foreach (var (schema, property, values) in Vocabularies)
        {
            Describe(document, schema, property, values);
        }
    }

    private static string[] NamesOf<TEnum>()
        where TEnum : struct, Enum =>
        [.. Enum.GetNames<TEnum>().Select(JsonNamingPolicy.SnakeCaseLower.ConvertName)];

    /// <summary>
    /// Every place a closed set is carried as a string, and what it may be.
    /// </summary>
    /// <remarks>
    /// The step segment's kind is the one that is not a domain enum: the
    /// contract deliberately flattens a union so the generated client does not
    /// have to narrow one, and the two values are stated here instead.
    /// </remarks>
    private static readonly (string Schema, string Property, string[] Values)[] Vocabularies =
    [
        ("RecipesIngredientContract", "unit", NamesOf<Unit>()),
        ("RecipesStepSegmentContract", "unit", NamesOf<Unit>()),
        ("RecipesRecipeDetail", "yieldKind", NamesOf<YieldKind>()),
        ("RecipesGetAllRecipeSummary", "yieldKind", NamesOf<YieldKind>()),
        ("RecipesUpdateRequest", "yieldKind", NamesOf<YieldKind>()),
        ("RecipesStepSegmentContract", "type", ["text", "ingredient"])
    ];

    private static void Describe(
        OpenApiDocument document,
        string schemaName,
        string propertyName,
        string[] values)
    {
        IOpenApiSchema? declared = null;
        IOpenApiSchema? found = null;

        document.Components?.Schemas?.TryGetValue(schemaName, out declared);
        declared?.Properties?.TryGetValue(propertyName, out found);

        if (found is not OpenApiSchema property)
        {
            throw new InvalidOperationException(
                $"'{schemaName}.{propertyName}' is not in the document. "
                + "The vocabulary list in OpenApiContractFixes is stale.");
        }

        property.Enum = [.. values.Select(value => (JsonNode)value)];
    }

    /// <summary>
    /// Describes the query parameters an endpoint accepts.
    /// </summary>
    /// <remarks>
    /// They are already declared, as endpoint metadata, so the guard middleware
    /// can reject anything else. Publishing the same declaration is what closes
    /// the loop: the generated client can send exactly what the server will
    /// accept, and nothing it would refuse.
    ///
    /// Without this the document says an operation takes no parameters at all,
    /// and the typed client simply cannot call it.
    /// </remarks>
    internal static void DescribeQueryParameters(
        OpenApiOperation operation,
        AllowedQueryParameters allowed)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(allowed);

        operation.Parameters ??= [];

        foreach (var name in allowed.Single)
        {
            operation.Parameters.Add(Query(name, allowed.IsInteger(name), repeatable: false));
        }

        foreach (var name in allowed.Repeatable)
        {
            operation.Parameters.Add(Query(name, allowed.IsInteger(name), repeatable: true));
        }
    }

    private static OpenApiParameter Query(string name, bool isInteger, bool repeatable)
    {
        var scalar = new OpenApiSchema
        {
            Type = isInteger ? JsonSchemaType.Integer : JsonSchemaType.String
        };

        return new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Query,
            Required = false,
            // Repeated rather than comma-joined: `?tag=vegan&tag=quick` is what
            // the guard accepts and what the query parser reads.
            Explode = repeatable,
            Schema = repeatable
                ? new OpenApiSchema { Type = JsonSchemaType.Array, Items = scalar }
                : scalar
        };
    }

    /// <summary>
    /// Describes the problem document Culina actually writes.
    /// </summary>
    /// <remarks>
    /// The framework's <c>ProblemDetails</c> schema stops at RFC 9457's five
    /// base members, but every Culina failure also carries the <c>code</c> the
    /// UI branches on, the <c>requestId</c> a person can quote in a bug report,
    /// and, for a rejected form, one entry per wrong field. A client generated
    /// from the unmodified schema cannot see any of them.
    /// </remarks>
    internal static void DescribeProblemExtensions(OpenApiDocument document)
    {
        if (document.Components?.Schemas is not { } schemas
            || !schemas.TryGetValue("ProblemDetails", out var declared)
            || declared is not OpenApiSchema problem)
        {
            return;
        }

        problem.Description =
            "An RFC 9457 problem document. Branch on `code`; `detail` is prose and will be reworded.";

        problem.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);

        problem.Properties["code"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "The stable, machine-readable reason. The only part a client may branch on."
        };

        problem.Properties["requestId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Description = "The correlation id of the request that failed, for support and logs."
        };

        problem.Properties["errors"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Array | JsonSchemaType.Null,
            Description = "One entry per contributing failure, so a form can mark every wrong field at once.",
            Items = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Required = new HashSet<string>(StringComparer.Ordinal) { "code", "detail" },
                Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                {
                    ["field"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String | JsonSchemaType.Null,
                        Description = "The field that was wrong, or null when the failure names none."
                    },
                    ["code"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String }
                }
            }
        };

        problem.Required = new HashSet<string>(StringComparer.Ordinal) { "code" };
    }
}
