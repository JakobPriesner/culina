namespace Api.Infrastructure;

/// <summary>The custom headers Culina sets or reads, named once.</summary>
internal static class CulinaHeaders
{
    /// <summary>
    /// Echoes the correlation id for the request. A user can paste it from an
    /// error message and an operator can find every log line and the whole
    /// trace.
    /// </summary>
    internal const string RequestId = "X-Request-Id";

    /// <summary>Carries the per-session CSRF token on unsafe requests.</summary>
    internal const string Csrf = "X-Culina-CSRF";
}
