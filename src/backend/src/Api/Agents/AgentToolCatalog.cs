using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Api.Agents;

/// <summary>Reads the agent's tools out of the app's own OpenAPI document.</summary>
/// <remarks>
/// The document is the contract the web app's client is generated from, so a tool's description, parameters and body
/// schema are the endpoint's own and cannot fall behind it. Schemas are inlined, because an input schema has to stand
/// on its own.
/// </remarks>
internal static class AgentToolCatalog
{
    private const string SchemaPrefix = "#/components/schemas/";

    internal static IReadOnlyDictionary<string, AgentTool> Read(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var schemas = document["components"]?["schemas"]?.AsObject() ?? [];
        var tools = new Dictionary<string, AgentTool>(StringComparer.Ordinal);

        foreach (var (path, item) in document["paths"]?.AsObject() ?? [])
        {
            foreach (var (method, node) in item?.AsObject() ?? [])
            {
                if (node is JsonObject operation
                    && operation["operationId"]?.GetValue<string>() is { } name
                    && AgentOperations.Allowed.Contains(name))
                {
                    tools[name] = ToTool(name, method.ToUpperInvariant(), path, operation, schemas);
                }
            }
        }

        return tools;
    }

    private static AgentTool ToTool(string name, string method, string path, JsonObject operation, JsonObject schemas)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        var pathParameters = new List<string>();
        var queryParameters = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var parameter in operation["parameters"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            var parameterName = parameter["name"]!.GetValue<string>();
            var schema = Inline(parameter["schema"], schemas, []);

            switch (parameter["in"]?.GetValue<string>())
            {
                case "path":
                    pathParameters.Add(parameterName);
                    break;
                case "query":
                    queryParameters[parameterName] = schema["type"]?.ToString() == "array";
                    break;
                default:
                    continue;
            }

            if (parameter["description"] is { } description)
            {
                schema["description"] = description.DeepClone();
            }

            properties[parameterName] = schema;

            if (parameter["required"]?.GetValue<bool>() == true)
            {
                required.Add(parameterName);
            }
        }

        var body = operation["requestBody"];

        if (body?["content"]?["application/json"]?["schema"] is { } bodySchema)
        {
            properties[AgentTool.Body] = Inline(bodySchema, schemas, []);

            if (body["required"]?.GetValue<bool>() == true)
            {
                required.Add(AgentTool.Body);
            }
        }

        var responses = operation["responses"]?.AsObject();
        var takesIfMatch = responses?.ContainsKey("412") == true || responses?.ContainsKey("428") == true;

        if (takesIfMatch)
        {
            properties[AgentTool.IfMatch] = new JsonObject
            {
                ["type"] = "string",
                ["description"] =
                    "The etag a read of this resource returned. A change without it is refused (428); "
                    + "if someone changed the resource since, it is refused as stale (412): read it again."
            };

            if (responses!.ContainsKey("428"))
            {
                required.Add(AgentTool.IfMatch);
            }
        }

        var inputSchema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };

        var tool = new Tool
        {
            Name = name,
            Title = operation["summary"]?.GetValue<string>(),
            Description = Describe(operation),
            InputSchema = JsonSerializer.SerializeToElement(inputSchema),
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = method == "GET",
                DestructiveHint = method is "PUT" or "DELETE",
                IdempotentHint = method is "GET" or "PUT" or "DELETE"
            }
        };

        return new AgentTool(tool, method, path, pathParameters, queryParameters, properties.ContainsKey(AgentTool.Body), takesIfMatch);
    }

    private static string Describe(JsonObject operation)
    {
        var summary = operation["summary"]?.GetValue<string>();
        var description = operation["description"]?.GetValue<string>();

        return string.Join("\n\n", new[] { summary, description }.Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    /// <summary>A copy of the schema with every reference replaced by what it refers to.</summary>
    /// <remarks>A reference back into a schema being inlined (none exist today) becomes an empty schema, not a loop.</remarks>
    private static JsonObject Inline(JsonNode? schema, JsonObject schemas, IReadOnlyList<string> within)
    {
        if (schema is not JsonObject node)
        {
            return [];
        }

        if (node["$ref"]?.GetValue<string>() is { } reference && reference.StartsWith(SchemaPrefix, StringComparison.Ordinal))
        {
            var target = reference[SchemaPrefix.Length..];
            var resolved = within.Contains(target) ? [] : Inline(schemas[target], schemas, [.. within, target]);

            // OpenAPI 3.1 lets a reference carry its own description beside it.
            foreach (var (key, value) in node.Where(pair => pair.Key != "$ref"))
            {
                resolved[key] = value?.DeepClone();
            }

            return resolved;
        }

        var copy = new JsonObject();

        foreach (var (key, value) in node)
        {
            copy[key] = InlineAny(value, schemas, within);
        }

        return copy;
    }

    private static JsonNode? InlineAny(JsonNode? value, JsonObject schemas, IReadOnlyList<string> within) => value switch
    {
        JsonObject child => Inline(child, schemas, within),
        JsonArray items => new JsonArray([.. items.Select(item => InlineAny(item, schemas, within))]),
        JsonNode leaf => leaf.DeepClone(),
        _ => null
    };
}
