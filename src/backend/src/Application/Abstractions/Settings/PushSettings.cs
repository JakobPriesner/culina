namespace Application.Abstractions.Settings;

/// <summary>Whether, and through which relay, the installed apps are told an import is ready.</summary>
/// <remarks>
/// Off unless an administrator names a relay: turning it on sends the notification's words to a service outside this
/// server (docs/native-app-design.md, 7.3). Web Push for browsers needs none of this.
/// </remarks>
public sealed record PushSettings
{
    /// <summary>The configuration section.</summary>
    public const string SectionName = "Push";

    /// <summary>The relay's address, or null for no app notifications.</summary>
    public Uri? RelayUrl { get; init; }

    /// <summary>Whether a relay is named.</summary>
    public bool RelayOn => RelayUrl is not null;

    /// <summary>Throws when the address could not be a relay: it must be https, with no credentials, query or fragment.</summary>
    public void Validate()
    {
        if (RelayUrl is { } relay
            && (!relay.IsAbsoluteUri || relay.Scheme != Uri.UriSchemeHttps || relay.UserInfo.Length > 0
                || relay.Query.Length > 0 || relay.Fragment.Length > 0))
        {
            throw new InvalidOperationException(
                $"Configuration Push__RelayUrl must be an https:// address with no user, query or fragment, but was '{relay}'.");
        }
    }
}
