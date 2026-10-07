namespace Contracts.Settings.GetAssistance;

/// <summary>
/// The models this instance can talk to, and which of them does what.
/// </summary>
/// <remarks>A contract type, never the settings record: an API key goes in and <c>apiKeyConfigured</c> comes out.</remarks>
public sealed record Response
{
    /// <summary>Whether the assistant is on at all.</summary>
    public required bool Enabled { get; init; }

    /// <summary>The providers this instance has been given, in a fixed order.</summary>
    public required IReadOnlyList<ConnectionContract> Connections { get; init; }

    /// <summary>Which provider and model does each job.</summary>
    public required IReadOnlyList<UseContract> Uses { get; init; }

    /// <summary>What the instance may spend in a month, or null for no ceiling.</summary>
    public decimal? MonthlyBudget { get; init; }

    /// <summary>What one person may spend of it, or null for no share.</summary>
    public decimal? PersonalBudget { get; init; }
}

/// <summary>One provider, and whether it is ready to be used.</summary>
public sealed record ConnectionContract
{
    /// <summary><c>gemini</c>, <c>openai</c> or <c>ollama</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>Whether a key has been entered. Never the key.</summary>
    public required bool ApiKeyConfigured { get; init; }

    /// <summary>Where the provider is, or empty for its own address.</summary>
    public required string BaseUrl { get; init; }

    /// <summary>
    /// Whether this connection has everything its provider needs.
    /// </summary>
    public required bool Usable { get; init; }
}

/// <summary>What does one job.</summary>
public sealed record UseContract
{
    /// <summary><c>improve</c>, <c>draft</c>, <c>read</c> or <c>draw</c>.</summary>
    public required string Capability { get; init; }

    /// <summary>Whether this job is offered at all.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Which provider does it, or empty if none was chosen.</summary>
    public required string Provider { get; init; }

    /// <summary>Which of its models, or empty for the current default.</summary>
    public required string Model { get; init; }

    /// <summary>
    /// The model that will actually be used when none is chosen.
    /// </summary>
    public required string DefaultModel { get; init; }
}
