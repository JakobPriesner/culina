using Domain.Sessions;

namespace Api.Agents;

/// <summary>Keeps agent sessions and everything else apart, in both directions.</summary>
/// <remarks>
/// An agent's token reaches the MCP endpoint and the <see cref="AgentOperations"/>, and only as a bearer header; a
/// browser's cookie or an app's token never reaches the MCP endpoint. So a leaked agent token cannot change a password
/// or invite anyone, and a page that can make a browser send its cookie cannot drive the MCP endpoint (which is why
/// the endpoint needs no Origin check of its own: nothing a browser attaches by itself is accepted there).
/// </remarks>
internal static class AgentAccess
{
    /// <summary>The reason logged when a session is refused here.</summary>
    internal const string Refused = "auth.agent_scope";

    /// <summary>Whether a session of this client may authenticate a request to this endpoint.</summary>
    internal static bool Admits(SessionClient client, bool bearer, Endpoint? endpoint)
    {
        var mcp = endpoint?.Metadata.GetMetadata<AgentServerEndpoint>() is not null;

        if (client != SessionClient.Agent)
        {
            return !mcp;
        }

        return bearer && (mcp || AgentOperations.Includes(endpoint));
    }
}
