namespace Domain.Shared;

/// <summary>
/// The kind of failure an <see cref="Error"/> represents. Transports switch on
/// this to choose a status code; nothing in the domain does.
/// </summary>
public enum ErrorType
{
    /// <summary>An unexpected failure. Maps to 500.</summary>
    Failure = 0,

    /// <summary>Input was rejected. Maps to 400.</summary>
    Validation = 1,

    /// <summary>A defect the caller cannot act on. Maps to 500.</summary>
    Problem = 2,

    /// <summary>The caller is not authenticated. Maps to 401.</summary>
    Unauthorized = 3,

    /// <summary>The caller is authenticated but not allowed. Maps to 403.</summary>
    Forbidden = 4,

    /// <summary>The resource is absent, or invisible to this caller. Maps to 404.</summary>
    NotFound = 5,

    /// <summary>The request conflicts with the current state. Maps to 409.</summary>
    Conflict = 6,

    /// <summary>An <c>If-Match</c> precondition failed. Maps to 412.</summary>
    PreconditionFailed = 7,

    /// <summary>The caller exceeded a rate limit. Maps to 429.</summary>
    RateLimited = 8,

    /// <summary>A dependency is temporarily unavailable. Maps to 503.</summary>
    Unavailable = 9
}
