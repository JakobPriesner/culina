using System.Text.Json.Nodes;
using Api.Infrastructure;
using Contracts.LogRecords;
using Contracts.Recipes;
using Microsoft.OpenApi;

namespace Api.Extensions;

/// <summary>Corrects places where the generated OpenAPI document describes the framework rather than the API.</summary>
internal static class OpenApiContractFixes
{
    /// <summary>Restores integers to being integers: the exporter emits "integer or digit string", which would give the client <c>number | string</c>.</summary>
    /// <remarks>Done on the finished document because a schema transformer's correction does not survive assembly.</remarks>
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
        // Schemas reference each other; without this a recipe containing a recipe recurses forever.
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

    /// <summary>Publishes the closed vocabularies the contract carries as plain strings, so the client need not redeclare them.</summary>
    /// <remarks>Listed explicitly, not matched by property name, which would eventually hit a non-vocabulary.</remarks>
    internal static void PublishVocabularies(OpenApiDocument document)
    {
        foreach (var (schema, property, values) in Vocabularies)
        {
            Describe(document, schema, property, values);
        }
    }

    // Every place a closed set is carried as a string. Values come from RecipeVocabulary, the list the reader and writer use.
    private static readonly (string Schema, string Property, IReadOnlyList<string> Values)[]
        Vocabularies =
    [
        // Units are deliberately not listed: the vocabulary is open, and a closed enum would stop the client sending
        // what the server accepts. The built-in list is published on the response that carries it.
        ("RecipesGetUnitsResponse", "builtIn", RecipeVocabulary.Units),
        ("RecipesRecipeDetail", "yieldKind", RecipeVocabulary.YieldKinds),
        ("RecipesGetAllRecipeSummary", "yieldKind", RecipeVocabulary.YieldKinds),
        ("RecipesUpdateRequest", "yieldKind", RecipeVocabulary.YieldKinds),
        ("RecipesGetSharedResponse", "yieldKind", RecipeVocabulary.YieldKinds),
        ("RecipesRecipeDetail", "language", RecipeVocabulary.Languages),
        ("RecipesGetSharedResponse", "language", RecipeVocabulary.Languages),
        ("RecipesUpdateRequest", "language", RecipeVocabulary.Languages),
        ("RecipesStepSegmentContract", "type", RecipeVocabulary.StepSegmentKinds),
        ("ShoppingItemContract", "section", RecipeVocabulary.ShoppingSections),
        ("ShoppingUpdateItemRequest", "section", RecipeVocabulary.ShoppingSections),
        ("LogRecordsCreateRecord", "event", LogRecordVocabulary.Events)
    ];

    private static void Describe(
        OpenApiDocument document,
        string schemaName,
        string propertyName,
        IReadOnlyList<string> values)
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

        // An array of codes carries the vocabulary on its items, not on the array.
        var target = property.Items as OpenApiSchema ?? property;

        target.Enum = [.. values.Select(value => (JsonNode)value)];
    }

    /// <summary>Describes the query parameters an endpoint accepts, from the same metadata the guard middleware enforces.</summary>
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
            // Repeated, not comma-joined: `?tag=vegan&tag=quick` is what the guard accepts.
            Explode = repeatable,
            Schema = repeatable
                ? new OpenApiSchema { Type = JsonSchemaType.Array, Items = scalar }
                : scalar
        };
    }

    /// <summary>Describes the problem document Culina writes: <c>code</c>, <c>requestId</c> and per-field <c>errors</c> beyond RFC 9457's base members.</summary>
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
