namespace Contracts.LogRecords;

/// <summary>What the web app reports, as the codes it reports them under.</summary>
/// <remarks>
/// A closed set; the server decides the level from the code, as a page choosing its own severity
/// could page somebody at night.
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
