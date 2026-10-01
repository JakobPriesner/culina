namespace Contracts.LogRecords;

/// <summary>
/// What the web app reports, as the codes it reports them under.
/// </summary>
/// <remarks>
/// A closed set, so the client cannot invent a kind of record and an operator
/// can filter on every one of them. The server decides the level from the code
/// rather than taking one from the browser: a page that could choose its own
/// severity could also page somebody at night.
/// </remarks>
public static class LogRecordVocabulary
{
    /// <summary>An exception nothing caught.</summary>
    public const string UncaughtError = "uncaught_error";

    /// <summary>A promise that failed with nobody waiting for it.</summary>
    public const string UnhandledRejection = "unhandled_rejection";

    /// <summary>A page or component that threw while drawing or loading.</summary>
    public const string RenderFailed = "render_failed";

    /// <summary>Something the content security policy blocked.</summary>
    public const string CspViolation = "csp_violation";

    /// <summary>The service worker failed, including at installing itself.</summary>
    public const string ServiceWorkerFailed = "service_worker_failed";

    /// <summary>Every code, in the order the document lists them.</summary>
    public static readonly IReadOnlyList<string> Events =
        [UncaughtError, UnhandledRejection, RenderFailed, CspViolation, ServiceWorkerFailed];
}
