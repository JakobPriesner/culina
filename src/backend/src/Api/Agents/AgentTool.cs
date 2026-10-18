using System.Text.Json;
using Domain.Shared;
using ModelContextProtocol.Protocol;

namespace Api.Agents;

/// <summary>One API operation offered to an agent as an MCP tool, and how a call's arguments become its request.</summary>
/// <param name="Protocol">The tool as the agent sees it: name, description, input schema.</param>
/// <param name="Method">The operation's HTTP method.</param>
/// <param name="PathTemplate">The operation's path, with <c>{name}</c> for each path parameter.</param>
/// <param name="PathParameters">The arguments that fill the path.</param>
/// <param name="QueryParameters">The arguments sent as query parameters, each with whether it may repeat.</param>
/// <param name="TakesBody">Whether the operation reads a JSON body, passed as the <c>body</c> argument.</param>
/// <param name="TakesIfMatch">Whether the operation checks If-Match, passed as the <c>ifMatch</c> argument.</param>
internal sealed record AgentTool(
    Tool Protocol,
    string Method,
    string PathTemplate,
    IReadOnlyList<string> PathParameters,
    IReadOnlyDictionary<string, bool> QueryParameters,
    bool TakesBody,
    bool TakesIfMatch)
{
    internal const string Body = "body";
    internal const string IfMatch = "ifMatch";

    /// <summary>The request a call's arguments describe, or what is wrong with them.</summary>
    /// <remarks>
    /// Only the shape is checked here; the endpoint validates the values as it would for any client. A path argument
    /// is held to one plain segment, so no value can lead the request to a different endpoint.
    /// </remarks>
    internal Result<AgentRequest> ToRequest(IDictionary<string, JsonElement> arguments)
    {
        if (arguments.Keys.FirstOrDefault(name => !Takes(name)) is { } unknown)
        {
            return AgentErrors.UnknownArgument(unknown);
        }

        var path = PathTemplate;

        foreach (var parameter in PathParameters)
        {
            if (!arguments.TryGetValue(parameter, out var value))
            {
                return AgentErrors.MissingArgument(parameter);
            }

            if (Scalar(value) is not { } segment || !IsPlainSegment(segment))
            {
                return AgentErrors.InvalidArgument(parameter, "a single path segment, without '/', '\\', '?', '#' or '%'");
            }

            path = path.Replace($"{{{parameter}}}", segment, StringComparison.Ordinal);
        }

        var query = new List<KeyValuePair<string, string?>>();

        foreach (var (name, repeatable) in QueryParameters)
        {
            if (!arguments.TryGetValue(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            var values = repeatable && value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().Select(Scalar).ToList()
                : [Scalar(value)];

            if (values.Contains(null))
            {
                return AgentErrors.InvalidArgument(name, repeatable ? "a string or number, or a list of them" : "a string, number or boolean");
            }

            query.AddRange(values.Select(text => KeyValuePair.Create(name, text)));
        }

        string? ifMatch = null;

        if (arguments.TryGetValue(IfMatch, out var etag))
        {
            if (etag.ValueKind != JsonValueKind.String)
            {
                return AgentErrors.InvalidArgument(IfMatch, "the etag string a read returned");
            }

            ifMatch = etag.GetString();
        }

        var body = arguments.TryGetValue(Body, out var json) ? json.GetRawText() : null;

        return new AgentRequest(Method, path, QueryString.Create(query), body, ifMatch);
    }

    private bool Takes(string argument) =>
        PathParameters.Contains(argument)
        || QueryParameters.ContainsKey(argument)
        || (TakesBody && argument == Body)
        || (TakesIfMatch && argument == IfMatch);

    private static string? Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null
    };

    private static bool IsPlainSegment(string segment) =>
        segment.Length > 0
        && segment is not ("." or "..")
        && !segment.Any(character => char.IsControl(character) || character is '/' or '\\' or '?' or '#' or '%');
}
