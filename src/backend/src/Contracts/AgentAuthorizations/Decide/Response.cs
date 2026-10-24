namespace Contracts.AgentAuthorizations.Decide;

/// <summary>Where the browser goes now that the request is answered.</summary>
public sealed record Response
{
    /// <summary>Whether the app was let in.</summary>
    public required bool Approved { get; init; }

    /// <summary>The app's return address, carrying the authorization code or <c>error=access_denied</c>; navigate to it.</summary>
    public required string RedirectUri { get; init; }
}
