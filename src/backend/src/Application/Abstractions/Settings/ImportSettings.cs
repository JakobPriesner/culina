namespace Application.Abstractions.Settings;

/// <summary>What the importer is allowed to reach.</summary>
/// <remarks>
/// The operator's decision, in the environment, not an admin toggle: it changes what the server can
/// be made to connect to. Off by default. Pasted-link imports refuse private addresses regardless;
/// a connected Tandoor is usually next door.
/// </remarks>
public sealed record ImportSettings
{
    /// <summary>The configuration section these values are read from.</summary>
    public const string SectionName = "Import";

    /// <summary>
    /// Whether a connected source may live on a private network; never applies to pasted-link
    /// imports.
    /// </summary>
    public bool AllowPrivateSourceAddresses { get; init; }

    /// <summary>Throws when any value would make the process unable to serve.</summary>
    /// <remarks>
    /// Nothing to check, so <c>static</c>; kept because every bootstrap settings record has one
    /// (architecture test).
    /// </remarks>
    public static void Validate()
    {
    }
}
