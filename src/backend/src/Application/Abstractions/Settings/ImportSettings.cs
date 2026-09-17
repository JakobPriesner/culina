namespace Application.Abstractions.Settings;

/// <summary>
/// What the importer is allowed to reach.
/// </summary>
/// <remarks>
/// <para>
/// One setting, and it exists because the two kinds of import genuinely differ.
/// Reading a recipe from a pasted link is something any account can ask for, at
/// any address, which is why that path refuses everything that is not the open
/// internet: without that, an account is a port scanner for whatever network
/// the container sits in.
/// </para>
/// <para>
/// A connected Tandoor is not that. Culina is self-hosted, and the single most
/// likely place somebody's Tandoor is running is the machine next to it — or
/// the same one. Refusing <c>http://tandoor.lan:8080</c> would refuse the
/// common case in the name of a threat the operator has already accepted by
/// running both.
/// </para>
/// <para>
/// So it is the operator's decision, taken once, in the environment, rather
/// than a toggle any admin can flip from a screen: it changes what the server
/// may be made to connect to, which is a property of the deployment and not of
/// the app. Off by default, because the safe answer has to be the one you get
/// by not thinking about it.
/// </para>
/// </remarks>
public sealed record ImportSettings
{
    /// <summary>The configuration section these values are read from.</summary>
    public const string SectionName = "Import";

    /// <summary>
    /// Whether a connected source may live on a private network.
    /// </summary>
    /// <remarks>
    /// Never applies to the pasted-link import, which refuses private addresses
    /// whatever this says. It is scoped to connections precisely because a
    /// connection is something a member of the household deliberately set up
    /// with a credential, not something a stranger can aim.
    /// </remarks>
    public bool AllowPrivateSourceAddresses { get; init; }

    /// <summary>Throws when any value would make the process unable to serve.</summary>
    /// <remarks>
    /// Nothing to check, and so <c>static</c>: a boolean read from configuration
    /// is either true or false, and the reader already refuses anything that is
    /// neither. The method is here because every bootstrap settings record has
    /// one and an architecture test asserts it — which is worth keeping even
    /// when one of them has nothing to say.
    /// </remarks>
    public static void Validate()
    {
    }
}
