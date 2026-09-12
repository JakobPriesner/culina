namespace Domain.Shared;

/// <summary>
/// The failure every optimistic write shares.
/// </summary>
/// <remarks>
/// Declared once rather than per module, because the meaning does not change
/// with the entity: whatever the caller was editing moved on, and it must read
/// the current state before writing again. Retrying blindly would overwrite
/// whoever got there first.
/// </remarks>
public static class ConcurrencyErrors
{
    /// <summary>The stored version no longer matches the one the caller saw.</summary>
    public static readonly Error VersionMismatch = new(
        "request.version_mismatch",
        "This item changed since you loaded it. Reload it and try again.",
        ErrorType.PreconditionFailed);
}
