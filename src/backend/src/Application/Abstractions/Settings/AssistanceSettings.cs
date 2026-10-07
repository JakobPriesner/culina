using Domain.Assistance;

namespace Application.Abstractions.Settings;

/// <summary>The models this instance can talk to, which does what, and the spending limits; off and empty by default.</summary>
/// <remarks>A connection says how to reach a provider (one per provider); a use says which provider and model does a job.</remarks>
public sealed record AssistanceSettings : IInstanceSettings<AssistanceSettings>
{
    /// <summary>Longer than any API key, and short enough to refuse a pasted file.</summary>
    public const int MaxApiKeyLength = 500;

    /// <summary>Longer than any model is named.</summary>
    public const int MaxModelLength = 100;

    /// <summary>The key this group is stored under.</summary>
    public static string GroupName => "assistance";

    /// <summary>Whether the assistant is on at all; lets an administrator switch it off without removing connections.</summary>
    public bool Enabled { get; set; }

    /// <summary>How to reach each provider this instance has been given.</summary>
    public IReadOnlyList<AssistanceConnection> Connections { get; set; } = [];

    /// <summary>Which provider and model does each job.</summary>
    public IReadOnlyList<CapabilityUse> Uses { get; set; } = [];

    /// <summary>The instance-wide monthly spending ceiling across all providers, or null for none.</summary>
    public decimal? MonthlyBudget { get; set; }

    /// <summary>What any one person may spend of it, or null for no share.</summary>
    public decimal? PersonalBudget { get; set; }

    /// <inheritdoc/>
    public void CopyFrom(AssistanceSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Enabled = other.Enabled;
        // Copied, not shared: this is a process-wide singleton.
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

    /// <summary>Whether the assistant is on, the job is on and has a use, and its provider is connected and can do it.</summary>
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

/// <summary>How to reach one provider; what counts as connected depends on the provider kind.</summary>
public sealed record AssistanceConnection
{
    /// <summary>Which provider: <c>gemini</c>, <c>openai</c> or <c>ollama</c>.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The encrypted API key. Never leaves the server; contracts expose <c>apiKeyConfigured</c> instead.</summary>
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

/// <summary>Which provider and model does one job; an empty model means the current default.</summary>
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
