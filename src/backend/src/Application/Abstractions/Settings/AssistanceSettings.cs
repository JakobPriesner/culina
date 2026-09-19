using Domain.Assistance;

namespace Application.Abstractions.Settings;

/// <summary>
/// The model this instance talks to, what it is allowed to do, and what it may
/// spend doing it.
/// </summary>
/// <remarks>
/// <para>
/// Instance settings rather than bootstrap, because every one of these is
/// something an administrator changes while the app runs — a model is
/// superseded, a budget turns out to be too small, a capability turns out to be
/// unwanted. None of them is worth a restart, and an API key that could only be
/// entered by editing a file on the server is a key nobody will rotate.
/// </para>
/// <para>
/// Everything here is off or empty by default. An instance that nobody
/// configures has no assistant, and an instance that has no assistant behaves
/// exactly as Culina behaved before this existed: the screens do not show a
/// disabled button, they show what they always showed.
/// </para>
/// </remarks>
public sealed record AssistanceSettings : IInstanceSettings<AssistanceSettings>
{
    /// <summary>Longer than any API key, and short enough to refuse a pasted file.</summary>
    public const int MaxApiKeyLength = 500;

    /// <summary>Longer than any model is named.</summary>
    public const int MaxModelLength = 100;

    /// <summary>The key this group is stored under.</summary>
    public static string GroupName => "assistance";

    /// <summary>
    /// Whether the assistant is on at all.
    /// </summary>
    /// <remarks>
    /// Separate from having a key, so an administrator can switch the whole
    /// thing off for an evening without throwing the key away and going to
    /// fetch it again.
    /// </remarks>
    public bool Enabled { get; set; }

    /// <summary>Which provider, as <c>gemini</c> or <c>openai</c>.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// The API key, encrypted.
    /// </summary>
    /// <remarks>
    /// Never leaves the server and never appears in a response: the contract
    /// types carry <c>apiKeyConfigured</c> instead, which is the whole reason
    /// they are contract types rather than this record serialised directly.
    /// </remarks>
    public string ProtectedApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Where the provider is.
    /// </summary>
    /// <remarks>
    /// An override for the hosted providers, which have an address of their own
    /// — it exists for the person running a gateway or a proxy. For Ollama it
    /// is not an override but the connection itself: a model on your own
    /// hardware is wherever you put it, and there is no default that could be
    /// right.
    /// </remarks>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The model that writes recipes.</summary>
    public string ComposeModel { get; set; } = string.Empty;

    /// <summary>The model that draws pictures.</summary>
    public string DrawModel { get; set; } = string.Empty;

    /// <summary>Whether it may rewrite a recipe somebody already has.</summary>
    public bool ImproveEnabled { get; set; } = true;

    /// <summary>Whether it may write one from an idea.</summary>
    public bool DraftEnabled { get; set; } = true;

    /// <summary>Whether it may read one out of a photograph or a block of text.</summary>
    public bool ReadEnabled { get; set; } = true;

    /// <summary>
    /// Whether it may draw a picture.
    /// </summary>
    /// <remarks>
    /// Off where the other three are on. It is by some distance the most
    /// expensive per use, and it is the one whose output is obviously not a
    /// photograph of the thing you cooked — so it is the one worth deciding
    /// about rather than inheriting.
    /// </remarks>
    public bool DrawEnabled { get; set; }

    /// <summary>
    /// What the whole instance may spend in a calendar month, or null for no
    /// ceiling.
    /// </summary>
    /// <remarks>
    /// In whatever currency the provider bills in, which is US dollars for both
    /// of the ones this knows. Not converted and not labelled with a symbol
    /// anywhere: a number this app converted would drift from the invoice, and
    /// the invoice is the thing being predicted.
    /// </remarks>
    public decimal? MonthlyBudget { get; set; }

    /// <summary>What any one person may spend of it, or null for no share.</summary>
    public decimal? PersonalBudget { get; set; }

    /// <inheritdoc/>
    public void CopyFrom(AssistanceSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Enabled = other.Enabled;
        Provider = other.Provider;
        ProtectedApiKey = other.ProtectedApiKey;
        BaseUrl = other.BaseUrl;
        ComposeModel = other.ComposeModel;
        DrawModel = other.DrawModel;
        ImproveEnabled = other.ImproveEnabled;
        DraftEnabled = other.DraftEnabled;
        ReadEnabled = other.ReadEnabled;
        DrawEnabled = other.DrawEnabled;
        MonthlyBudget = other.MonthlyBudget;
        PersonalBudget = other.PersonalBudget;
    }

    /// <summary>Whether a key has been entered.</summary>
    public bool HasApiKey => ProtectedApiKey.Length > 0;

    /// <summary>Which provider, or null when the stored name is not one this knows.</summary>
    public AssistantKind? Kind => AssistantKind.Parse(Provider);

    /// <summary>
    /// Whether the connection has everything that provider needs.
    /// </summary>
    /// <remarks>
    /// Not "has a key", which is what this used to mean and what it can no
    /// longer mean: a model on your own machine has no key and is perfectly
    /// well connected without one, while it is the one that is useless without
    /// an address.
    /// </remarks>
    public bool IsConnected =>
        Kind is { } kind
        && (!kind.NeedsApiKey || HasApiKey)
        && (!kind.NeedsAddress || BaseUrl.Length > 0);

    /// <summary>Whether this capability may be used right now.</summary>
    /// <param name="capability">Which one.</param>
    /// <remarks>
    /// Asked of the settings rather than worked out at each call site, because
    /// "on, connected, allowed here, and possible for this provider" is four
    /// conditions and there are four places that need all four.
    /// </remarks>
    public bool Allows(Capability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (!Enabled || !IsConnected)
        {
            return false;
        }

        if (capability == Capability.Draw)
        {
            return DrawEnabled && Kind is { CanDraw: true };
        }

        return capability.Code switch
        {
            "improve" => ImproveEnabled,
            "draft" => DraftEnabled,
            "read" => ReadEnabled,
            _ => false
        };
    }
}
