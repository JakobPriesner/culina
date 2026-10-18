namespace Api.Agents;

/// <summary>An API request a tool call stands for.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The path, its parameters filled in.</param>
/// <param name="Query">The query string.</param>
/// <param name="Json">The JSON body, if the call carried one.</param>
/// <param name="IfMatch">The etag the change is conditional on, if the call carried one.</param>
internal sealed record AgentRequest(string Method, string Path, QueryString Query, string? Json, string? IfMatch);
