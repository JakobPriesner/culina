namespace Api.Agents;

/// <summary>What the API answered a tool call's request.</summary>
/// <param name="Status">The status code.</param>
/// <param name="ETag">The response's ETag, which a later change passes back as If-Match.</param>
/// <param name="Location">Where a created resource lives.</param>
/// <param name="ContentType">The body's media type.</param>
/// <param name="Body">The body.</param>
internal sealed record AgentReply(int Status, string? ETag, string? Location, string? ContentType, byte[] Body);
