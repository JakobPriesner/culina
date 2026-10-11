namespace Contracts.Settings.UpdateAssistance;

/// <summary>The assistant configuration to apply, all at once: one screen with one Save, so no half-applied state.</summary>
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

    /// <summary>The administrator's own prices. Anything left out falls back to Culina's list.</summary>
    public required IReadOnlyList<PriceRequest> Prices { get; init; }
}

/// <summary>One provider to connect, or to keep connected.</summary>
public sealed record ConnectionRequest
{
    /// <summary><c>gemini</c>, <c>openai</c> or <c>ollama</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>A new API key, or null to keep the stored one. An empty string removes it.</summary>
    /// <remarks>
    /// Null with a different <c>baseUrl</c> is refused with <c>assistance.api_key_required</c>: a stored key is only sent to the address it was saved for.
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

/// <summary>What the administrator pays for one model; leave a part empty to keep Culina's price for it.</summary>
public sealed record PriceRequest
{
    /// <summary>The model, exactly as the provider names it (not a prefix).</summary>
    public required string Model { get; init; }

    /// <summary>US dollars per million input tokens; used only once <c>outputPerMillion</c> is given too.</summary>
    public decimal? InputPerMillion { get; init; }

    /// <summary>US dollars per million output tokens.</summary>
    public decimal? OutputPerMillion { get; init; }

    /// <summary>US dollars per image.</summary>
    public decimal? PerImage { get; init; }
}
