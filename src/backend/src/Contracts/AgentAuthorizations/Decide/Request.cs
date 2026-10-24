namespace Contracts.AgentAuthorizations.Decide;

/// <summary>The caller's answer to an app asking to connect an AI agent.</summary>
public sealed record Request
{
    /// <summary>True to let the app's agent into the caller's account, false to refuse.</summary>
    public required bool Approved { get; init; }
}
