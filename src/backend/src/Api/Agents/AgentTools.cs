using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Api.Extensions;
using Api.Infrastructure;
using Domain.Shared;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using ModelContextProtocol.Protocol;

namespace Api.Agents;

/// <summary>Lists the agent's tools and answers a call to one.</summary>
/// <remarks>
/// A call is one API request, made as the agent, and its answer is what the API said: <c>status</c>, <c>etag</c> and
/// <c>location</c> when there are any, and the JSON <c>body</c>, which for a failure is the usual problem document.
/// </remarks>
internal sealed class AgentTools(
    [FromKeyedServices(OpenApiExtensions.DocumentName)] IOpenApiDocumentProvider documents,
    AgentRequests requests,
    IHttpContextAccessor http)
{
    private static readonly Dictionary<string, JsonElement> NoArguments = [];

    // Built on the first use rather than at startup: the document needs every endpoint mapped.
    private readonly Lazy<Task<IReadOnlyDictionary<string, AgentTool>>> catalog = new(() => ReadAsync(documents));

    internal async ValueTask<ListToolsResult> ListAsync()
    {
        var tools = await catalog.Value.ConfigureAwait(false);

        return new ListToolsResult { Tools = [.. tools.Values.Select(tool => tool.Protocol)] };
    }

    internal async ValueTask<CallToolResult> CallAsync(CallToolRequestParams call)
    {
        ArgumentNullException.ThrowIfNull(call);

        // Stateless transport: a call is answered within the HTTP request that carried it.
        var asking = http.HttpContext
            ?? throw new InvalidOperationException("A tool call is answered within the HTTP request that carried it.");

        var tools = await catalog.Value.ConfigureAwait(false);

        var request = tools.TryGetValue(call.Name, out var tool)
            ? tool.ToRequest(call.Arguments ?? NoArguments)
            : AgentErrors.UnknownTool(call.Name);

        return await request.Match(
            async valid => Answer(await requests.SendAsync(asking, valid).ConfigureAwait(false)),
            error => Task.FromResult(Refuse(error))).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyDictionary<string, AgentTool>> ReadAsync(IOpenApiDocumentProvider documents)
    {
        var document = await documents.GetOpenApiDocumentAsync().ConfigureAwait(false);

        using var text = new StringWriter(CultureInfo.InvariantCulture);

        // 3.1, whose schemas are JSON Schema, which is what a tool's input schema is.
        document.SerializeAsV31(new OpenApiJsonWriter(text));

        return AgentToolCatalog.Read(JsonNode.Parse(text.ToString())!.AsObject());
    }

    private static CallToolResult Answer(AgentReply reply)
    {
        var answer = new JsonObject { ["status"] = reply.Status };

        if (reply.ETag is { } etag)
        {
            answer["etag"] = etag;
        }

        if (reply.Location is { } location)
        {
            answer["location"] = location;
        }

        if (reply.Body.Length > 0 && reply.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            answer["body"] = JsonNode.Parse(reply.Body);
        }

        return Result(answer, reply.Status >= StatusCodes.Status400BadRequest);
    }

    // Shaped like the API's own answers, so an agent reads one kind of failure.
    private static CallToolResult Refuse(Error error) =>
        Result(
            new JsonObject
            {
                ["status"] = ErrorStatusCodes.Of(error.Type),
                ["body"] = new JsonObject { ["code"] = error.Code, ["detail"] = error.Description }
            },
            isError: true);

    private static CallToolResult Result(JsonObject answer, bool isError) => new()
    {
        Content = [new TextContentBlock { Text = answer.ToJsonString() }],
        IsError = isError
    };
}
