using Application.Settings;

namespace Api.Infrastructure;

/// <summary>The status a saved server setting answers with.</summary>
internal static class ServerChangeResults
{
    /// <summary>
    /// <c>204</c> when nothing differed and nothing happened; <c>202</c> when
    /// the settings were saved and the server is restarting to use them, which
    /// is work accepted and not yet done — the client waits for
    /// <c>GET /api/v1/setup</c> to report a new <c>startedAt</c>.
    /// </summary>
    internal static IResult Of(ServerChange change) =>
        change == ServerChange.Restarting ? Results.Accepted() : Results.NoContent();
}
