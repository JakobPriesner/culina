namespace Contracts.Settings.GetAssistanceUsage;

/// <summary>What the assistant has cost this month.</summary>
/// <remarks>A month, to match the monthly budget it is shown beside.</remarks>
public sealed record Response
{
    /// <summary>When the period being reported started.</summary>
    public required DateTimeOffset Since { get; init; }

    /// <summary>Everything spent, where a price was known.</summary>
    public required decimal TotalCost { get; init; }

    /// <summary>The instance's ceiling, or null when there is none.</summary>
    public decimal? MonthlyBudget { get; init; }

    /// <summary>Everything sent.</summary>
    public required long TotalInputTokens { get; init; }

    /// <summary>Everything received.</summary>
    public required long TotalOutputTokens { get; init; }

    /// <summary>Everything drawn.</summary>
    public required long TotalPictures { get; init; }

    /// <summary>How many calls used a model with no known price; reported so a total that is missing something says so.</summary>
    public required int Unpriced { get; init; }

    /// <summary>Who spent what.</summary>
    public required IReadOnlyList<PersonUsageContract> ByPerson { get; init; }

    /// <summary>What it went on.</summary>
    public required IReadOnlyList<CapabilityUsageContract> ByCapability { get; init; }

    /// <summary>Which models, and whose price each figure was worked out with.</summary>
    public required IReadOnlyList<ModelUsageContract> ByModel { get; init; }

    /// <summary>When Culina's own price list was last checked against the providers, for figures priced from it.</summary>
    public required DateOnly PricesRecordedOn { get; init; }
}

/// <summary>One person's spend this month.</summary>
public sealed record PersonUsageContract
{
    /// <summary>Who.</summary>
    public required Guid UserId { get; init; }

    /// <summary>What to call them on screen.</summary>
    public required string DisplayName { get; init; }

    /// <summary>How many times they asked.</summary>
    public required int Calls { get; init; }

    /// <summary>What it came to.</summary>
    public required decimal Cost { get; init; }
}

/// <summary>One capability's spend this month.</summary>
public sealed record CapabilityUsageContract
{
    /// <summary><c>improve</c>, <c>draft</c>, <c>read</c> or <c>draw</c>.</summary>
    public required string Capability { get; init; }

    /// <summary>How many times it was used.</summary>
    public required int Calls { get; init; }

    /// <summary>What it came to.</summary>
    public required decimal Cost { get; init; }
}

/// <summary>One model's settled calls this month, at one source of price.</summary>
/// <remarks>A model whose price changed source during the month appears once per source.</remarks>
public sealed record ModelUsageContract
{
    /// <summary><c>gemini</c>, <c>openai</c> or <c>ollama</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>Which model.</summary>
    public required string Model { get; init; }

    /// <summary><c>culina</c> for Culina's own list, <c>administrator</c> for a price set here, or null when the model had no price.</summary>
    public string? PricedBy { get; init; }

    /// <summary>How many calls.</summary>
    public required int Calls { get; init; }

    /// <summary>What they came to.</summary>
    public required decimal Cost { get; init; }
}
