using System.Text.Json;
using System.Text.Json.Nodes;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Guards the committed OpenAPI document, because it is the contract the
/// frontend client is generated from.
/// </summary>
/// <remarks>
/// These assertions exist because the schema exporter describes the framework's
/// defaults rather than Culina's API in two specific ways, and both corrections
/// are invisible until a generated type is wrong.
/// </remarks>
public class OpenApiDocumentTests
{
    private static readonly JsonNode Document = Load();

    [Fact]
    public void Schemas_ShouldDescribeWholeNumbersAsIntegers_WhenExported()
    {
        // Arrange
        var offenders = new List<string>();

        // Act
        Walk(Document["components"]?["schemas"], "components.schemas", (path, schema) =>
        {
            var types = TypesOf(schema);

            if (types.Contains("string") && (types.Contains("integer") || types.Contains("number")))
            {
                offenders.Add(path);
            }
        });

        // Assert
        Assert.Empty(offenders);
    }

    [Fact]
    public void ProblemDetails_ShouldDescribeTheExtensionsEveryFailureCarries_WhenExported()
    {
        // Arrange
        var problem = Document["components"]?["schemas"]?["ProblemDetails"];

        // Act
        var properties = problem?["properties"];

        // Assert
        Assert.NotNull(properties);
        Assert.NotNull(properties["code"]);
        Assert.NotNull(properties["requestId"]);
        Assert.NotNull(properties["errors"]);
    }

    [Fact]
    public void ProblemDetails_ShouldRequireTheCode_WhenExported()
    {
        // Arrange
        var problem = Document["components"]?["schemas"]?["ProblemDetails"];

        // Act
        var required = problem?["required"]?.AsArray().Select(value => (string?)value);

        // Assert
        Assert.Contains("code", required ?? []);
    }

    [Fact]
    public void Paths_ShouldAllCarryTheVersionPrefix_WhenExported()
    {
        // Arrange
        var paths = Document["paths"]!.AsObject().Select(entry => entry.Key);

        // Act & Assert
        Assert.All(paths, path => Assert.StartsWith("/api/v1/", path, StringComparison.Ordinal));
    }

    [Fact]
    public void Document_ShouldNameNoServer_WhenExported()
    {
        // Arrange & Act
        var servers = Document["servers"]?.AsArray();

        // Assert
        // The app is always served from the same origin as its client, and the
        // paths already carry the prefix, so a server entry would double it.
        Assert.True(servers is null or { Count: 0 });
    }

    /// <summary>
    /// The declared types of a schema.
    /// </summary>
    /// <remarks>
    /// A "type" key is not proof of a schema: <c>ProblemDetails.properties</c>
    /// has a property called <c>type</c>, whose value is a schema object. Only
    /// a string or an array of strings is a type declaration.
    /// </remarks>
    private static IReadOnlyCollection<string> TypesOf(JsonNode node) =>
        node["type"] switch
        {
            JsonArray many => [.. many.OfType<JsonValue>().Select(value => value.ToString())],
            JsonValue single => [single.ToString()],
            _ => []
        };

    /// <summary>Visits every schema in the document, however deeply nested.</summary>
    private static void Walk(JsonNode? node, string path, Action<string, JsonNode> visit)
    {
        switch (node)
        {
            case JsonObject entries:
                visit(path, entries);

                foreach (var (key, value) in entries)
                {
                    Walk(value, $"{path}.{key}", visit);
                }

                break;

            case JsonArray items:
                for (var index = 0; index < items.Count; index++)
                {
                    Walk(items[index], $"{path}[{index}]", visit);
                }

                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Reads the committed document rather than generating one, so the file the
    /// frontend actually generates from is the file under test.
    /// </summary>
    private static JsonNode Load()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "openapi", "Api.json");

            if (File.Exists(candidate))
            {
                return JsonNode.Parse(File.ReadAllText(candidate))
                    ?? throw new InvalidOperationException($"'{candidate}' is not a JSON document.");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "openapi/Api.json was not found. Run `make openapi` to export it.");
    }
}
