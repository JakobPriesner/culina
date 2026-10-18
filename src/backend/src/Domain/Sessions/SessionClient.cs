namespace Domain.Sessions;

/// <summary>What a session was started from: a browser holds it as a cookie, an app as a bearer token.</summary>
public enum SessionClient
{
    /// <summary>A web browser, signed in with the session cookie.</summary>
    Browser = 0,

    /// <summary>The iOS app, which keeps the token in the Keychain.</summary>
    Ios = 1,

    /// <summary>The Android app, which keeps the token in the Keystore.</summary>
    Android = 2,

    /// <summary>Another Culina instance importing this person's recipes, which keeps the token encrypted with its connection.</summary>
    Instance = 3,

    /// <summary>
    /// An AI agent using the MCP server, which keeps the token in its own configuration. It reaches only the MCP
    /// endpoint and the operations offered there as tools, never the rest of the account.
    /// </summary>
    Agent = 4
}
