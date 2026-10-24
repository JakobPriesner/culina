namespace Contracts.AgentAuthorizations.GetById;

/// <summary>An app asking to connect an AI agent to the caller's account, as the consent screen shows it.</summary>
/// <remarks>Every string is the app's own word: show it as text, never as markup.</remarks>
public sealed record Response
{
    /// <summary>The request's id, for answering it.</summary>
    public required Guid AuthorizationId { get; init; }

    /// <summary>The app's OAuth <c>client_id</c>: the address of its metadata document, or the id it registered with.</summary>
    public required string ClientId { get; init; }

    /// <summary>What the app calls itself.</summary>
    public required string ClientName { get; init; }

    /// <summary>The host the app's metadata document came from, which vouches for its name; null when nothing does (the app registered itself).</summary>
    public string? VerifiedHost { get; init; }

    /// <summary>The host the browser is sent back to, with the code.</summary>
    public required string RedirectHost { get; init; }

    /// <summary>Whether that is this computer (a loopback address), which any program on it can listen on.</summary>
    public required bool RedirectsToThisComputer { get; init; }

    /// <summary>Until when the request can be answered.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
