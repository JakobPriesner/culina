namespace Contracts;

/// <summary>Which generation of the API contract this build speaks.</summary>
/// <remarks>
/// The website ships with its server, so it always agrees with it. An installed app does not: it is
/// built once and then talks to whatever version each self-hosted instance runs. Changes inside
/// <c>/api/v1</c> stay additive; raise this only for one an app built for the previous value could
/// not read, and widen the range the app accepts (src/frontend/src/lib/api/instance.ts) in the
/// same change.
/// </remarks>
public static class ApiContract
{
    /// <summary>The current generation.</summary>
    public const int Version = 1;
}
