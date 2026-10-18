namespace Contracts.Sessions.SignIn;

/// <summary>Credentials.</summary>
public sealed record Request
{
    /// <summary>The address the account was registered with.</summary>
    public required string Email { get; init; }

    /// <summary>The account's password.</summary>
    public required string Password { get; init; }

    /// <summary>
    /// <c>browser</c> (the default) for the session cookie; <c>ios</c> or <c>android</c> for an app,
    /// <c>instance</c> for another Culina importing this person's recipes, or <c>agent</c> for an AI agent using
    /// the MCP server at <c>/api/mcp</c>, each of which receives the session token in the response and sends it as
    /// <c>Authorization: Bearer</c>. An agent's token works only there.
    /// </summary>
    public string? Client { get; init; }
}
