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
