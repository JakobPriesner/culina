namespace Contracts.Settings.UpdateAssistance;

/// <summary>The assistant configuration to apply.</summary>
/// <remarks>
/// The whole thing at once rather than a route per connection: it is one
/// screen with one Save, and a half-applied configuration — a use pointing at a
/// provider whose connection did not save — is a state nobody should be able to
/// reach.
/// </remarks>
public sealed record Request
{
    /// <summary>Whether the assistant is on at all.</summary>
    public required bool Enabled { get; init; }

    /// <summary>The providers to keep. Anything left out is disconnected.</summary>
    public required IReadOnlyList<ConnectionRequest> Connections { get; init; }

    /// <summary>Which provider and model does each job.</summary>
    public required IReadOnlyList<UseRequest> Uses { get; init; }

    /// <summary>What the instance may spend in a month, or null for no ceiling.</summary>
    public decimal? MonthlyBudget { get; init; }

    /// <summary>What one person may spend of it, or null for no share.</summary>
    public decimal? PersonalBudget { get; init; }
}

/// <summary>One provider to connect, or to keep connected.</summary>
public sealed record ConnectionRequest
{
    /// <summary><c>gemini</c>, <c>openai</c> or <c>ollama</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>
    /// A new API key, or null to keep the one already stored.
    /// </summary>
    /// <remarks>
    /// Three states rather than two, because a settings form that is saved for
    /// an unrelated reason must not wipe a key. Null — the field left out —
    /// means leave it alone, which is what a form sends when its key box is
    /// empty because there was nothing to show in it. An empty string means
    /// take it away. Anything else replaces it.
    /// </remarks>
    public string? ApiKey { get; init; }

    /// <summary>Where the provider is, or empty for its own address.</summary>
    public required string BaseUrl { get; init; }
}

/// <summary>What should do one job.</summary>
public sealed record UseRequest
{
    /// <summary><c>improve</c>, <c>draft</c>, <c>read</c> or <c>draw</c>.</summary>
    public required string Capability { get; init; }

    /// <summary>Whether this job is offered at all.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Which provider does it.</summary>
    public required string Provider { get; init; }

    /// <summary>Which of its models, or empty for the current default.</summary>
    public required string Model { get; init; }
}
