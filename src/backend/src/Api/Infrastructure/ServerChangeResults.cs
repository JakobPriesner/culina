using Application.Settings;

namespace Api.Infrastructure;

/// <summary>The status a saved server setting answers with.</summary>
internal static class ServerChangeResults
{
    /// <summary><c>204</c> when nothing differed; <c>202</c> when saved and the server is restarting to apply it.</summary>
    internal static IResult Of(ServerChange change) =>
        change == ServerChange.Restarting ? Results.Accepted() : Results.NoContent();
}
