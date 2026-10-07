namespace Domain.Shared;

/// <summary>The failure every optimistic write shares: what the caller was editing moved on, so it must reload; retrying blindly would overwrite the other writer.</summary>
public static class ConcurrencyErrors
{
    /// <summary>The stored version no longer matches the one the caller saw.</summary>
    public static readonly Error VersionMismatch = new(
        "request.version_mismatch",
        "This item changed since you loaded it. Reload it and try again.",
        ErrorType.PreconditionFailed);
}
