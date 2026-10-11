namespace Contracts.Settings.UpdateAssistance;

/// <summary>The models this instance can talk to, and which does what. An API key goes in and <c>apiKeyConfigured</c> comes out, never the value.</summary>
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

    /// <summary>The administrator's own prices, which win over Culina's list for the models they name.</summary>
    public required IReadOnlyList<PriceContract> Prices { get; init; }

    /// <summary>When Culina's own price list was last checked against the providers.</summary>
    public required DateOnly PricesRecordedOn { get; init; }
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

    /// <summary>Whether this connection has everything its provider needs: a local model needs an address but no key.</summary>
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

    /// <summary>The model used when none is chosen; the server decides, so the form can show it as a placeholder.</summary>
    public required string DefaultModel { get; init; }

    /// <summary>What Culina's own list says the model this job uses costs, or null when it has no price for it (or it is local and free).</summary>
    public ListedPriceContract? ListedPrice { get; init; }
}

/// <summary>What the administrator pays for one model; an empty part is left to Culina's list.</summary>
public sealed record PriceContract
{
    /// <summary>The model, exactly as the provider names it.</summary>
    public required string Model { get; init; }

    /// <summary>US dollars per million input tokens.</summary>
    public decimal? InputPerMillion { get; init; }

    /// <summary>US dollars per million output tokens.</summary>
    public decimal? OutputPerMillion { get; init; }

    /// <summary>US dollars per image.</summary>
    public decimal? PerImage { get; init; }
}

/// <summary>A model's price on Culina's own list; a part it does not know is null.</summary>
public sealed record ListedPriceContract
{
    /// <summary>US dollars per million input tokens.</summary>
    public decimal? InputPerMillion { get; init; }

    /// <summary>US dollars per million output tokens.</summary>
    public decimal? OutputPerMillion { get; init; }

    /// <summary>US dollars per image.</summary>
    public decimal? PerImage { get; init; }
}
