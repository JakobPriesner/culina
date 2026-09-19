namespace Contracts.Settings.GetAssistanceUsage;

/// <summary>What the assistant has cost this month.</summary>
/// <remarks>
/// A month rather than a configurable window, because the budget is a monthly
/// one and a usage screen measuring a different period from the cap it is shown
/// beside would be two numbers nobody can reconcile.
/// </remarks>
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

    /// <summary>
    /// How many calls used a model this app has no price for.
    /// </summary>
    /// <remarks>
    /// Reported rather than folded in, so a total that is missing something
    /// says that it is. Zero on any instance using a model this knows about.
    /// </remarks>
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
