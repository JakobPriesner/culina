namespace Api.Infrastructure;

/// <summary>The custom headers Culina sets or reads, named once.</summary>
internal static class CulinaHeaders
{
    /// <summary>Echoes the request's correlation id, so a user can paste it from an error and an operator can find the trace.</summary>
    internal const string RequestId = "X-Request-Id";

    /// <summary>Carries the per-session CSRF token on unsafe requests.</summary>
    internal const string Csrf = "X-Culina-CSRF";
}
