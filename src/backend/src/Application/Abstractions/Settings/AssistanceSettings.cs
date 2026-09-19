using Domain.Assistance;

namespace Application.Abstractions.Settings;

/// <summary>
/// The models this instance can talk to, which of them does what, and what they
/// may spend doing it.
/// </summary>
/// <remarks>
/// <para>
/// Several connections rather than one, because the providers are not
/// interchangeable and a household that has more than one wants each for what
/// it is good at. Reading a cookbook photograph wants a model with good vision;
/// tidying up wording wants a cheap one that will be asked twenty times an
/// evening; drawing wants one that draws at all, which the model on your own
/// hardware does not. Making that one global choice meant picking the provider
/// that was least bad at everything.
/// </para>
/// <para>
/// So a connection says <em>how to reach a provider</em> and a use says
/// <em>which provider and model does this job</em>. They are separate lists on
/// purpose: a connection is set up once and a use is changed whenever somebody
/// reads that a new model is better.
/// </para>
/// <para>
/// One connection per provider, not per name. Two Ollama boxes or a direct
/// OpenAI key alongside a gateway would need connections to be named and
/// managed, and nobody has asked for that — this is the shape that satisfies
/// "several providers at once" without inventing the screen for the other
/// thing.
/// </para>
/// <para>
/// Everything is off and empty by default. An instance that nobody configures
/// has no assistant, and behaves exactly as Culina behaved before there was one.
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
    /// Separate from having connections, so an administrator can switch the
    /// whole thing off for an evening without taking any of them apart.
    /// </remarks>
    public bool Enabled { get; set; }

    /// <summary>How to reach each provider this instance has been given.</summary>
    public IReadOnlyList<AssistanceConnection> Connections { get; set; } = [];

    /// <summary>Which provider and model does each job.</summary>
    public IReadOnlyList<CapabilityUse> Uses { get; set; } = [];

    /// <summary>
    /// What the whole instance may spend in a calendar month, or null for no
    /// ceiling.
    /// </summary>
    /// <remarks>
    /// One budget across every provider rather than one each. The question it
    /// answers is "what is this costing me", and that question does not care
    /// which company sent the invoice.
    /// </remarks>
    public decimal? MonthlyBudget { get; set; }

    /// <summary>What any one person may spend of it, or null for no share.</summary>
    public decimal? PersonalBudget { get; set; }

    /// <inheritdoc/>
    public void CopyFrom(AssistanceSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Enabled = other.Enabled;
        // Copied rather than shared: this is a process-wide singleton, and two
        // of them pointing at one list is one of them changing under the other.
        Connections = [.. other.Connections.Select(one => one with { })];
        Uses = [.. other.Uses.Select(one => one with { })];
        MonthlyBudget = other.MonthlyBudget;
        PersonalBudget = other.PersonalBudget;
    }

    /// <summary>How to reach this provider, or null if it is not connected.</summary>
    /// <param name="kind">Which provider.</param>
    public AssistanceConnection? ConnectionFor(AssistantKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);

        return Connections.FirstOrDefault(one => one.Provider == kind.Code);
    }

    /// <summary>What was chosen for this job, or null if nothing was.</summary>
    /// <param name="capability">Which job.</param>
    public CapabilityUse? UseFor(Capability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        return Uses.FirstOrDefault(one => one.Capability == capability.Code);
    }

    /// <summary>Whether anything at all is connected and usable.</summary>
    public bool IsConnected => Connections.Any(one => one.IsUsable);

    /// <summary>
    /// Whether this capability can be used right now.
    /// </summary>
    /// <remarks>
    /// Five conditions, in one place, because there are four call sites that
    /// need all five: the assistant is on, this job is switched on, something
    /// was chosen for it, that provider is actually connected, and that provider
    /// can do this job at all.
    /// </remarks>
    /// <param name="capability">Which job.</param>
    public bool Allows(Capability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (!Enabled || UseFor(capability) is not { Enabled: true } use)
        {
            return false;
        }

        if (AssistantKind.Parse(use.Provider) is not { } kind)
        {
            return false;
        }

        return ConnectionFor(kind) is { IsUsable: true }
            && (capability != Capability.Draw || kind.CanDraw);
    }
}

/// <summary>
/// How to reach one provider.
/// </summary>
/// <remarks>
/// What "connected" means differs by provider, which is why this asks the kind
/// rather than checking for a key: the hosted two need a credential and know
/// their own address, and a model on your own hardware is the other way round.
/// </remarks>
public sealed record AssistanceConnection
{
    /// <summary>Which provider: <c>gemini</c>, <c>openai</c> or <c>ollama</c>.</summary>
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

    /// <summary>Where the provider is, or empty for its own address.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Whether a key has been entered.</summary>
    public bool HasApiKey => ProtectedApiKey.Length > 0;

    /// <summary>Whether this connection has everything its provider needs.</summary>
    public bool IsUsable =>
        AssistantKind.Parse(Provider) is { } kind
        && (!kind.NeedsApiKey || HasApiKey)
        && (!kind.NeedsAddress || BaseUrl.Length > 0);
}

/// <summary>
/// Which provider and model does one job.
/// </summary>
/// <remarks>
/// The model is a string an administrator may leave empty, and empty means
/// "whatever Culina currently defaults to for that provider" rather than
/// "whatever this build shipped believing". Pinning one is for somebody who has
/// a reason to.
/// </remarks>
public sealed record CapabilityUse
{
    /// <summary><c>improve</c>, <c>draft</c>, <c>read</c> or <c>draw</c>.</summary>
    public string Capability { get; set; } = string.Empty;

    /// <summary>Whether this job is offered at all.</summary>
    public bool Enabled { get; set; }

    /// <summary>Which provider does it.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Which of its models, or empty for the current default.</summary>
    public string Model { get; set; } = string.Empty;
}
