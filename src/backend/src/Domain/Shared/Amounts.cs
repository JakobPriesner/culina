namespace Domain.Shared;

/// <summary>The decimal places amounts and servings keep, matching the <c>numeric(10, 3)</c> columns that store them.</summary>
/// <remarks>
/// Rounding here, before the checks, means a value that is accepted is the value that comes back
/// from storage, and one that would store as zero is refused instead of corrupting the row.
/// </remarks>
public static class Amounts
{
    /// <summary>How many decimal places are stored.</summary>
    public const int Decimals = 3;

    /// <summary>The smallest amount that survives storing.</summary>
    public const decimal Min = 0.001m;

    /// <summary>Rounds to the stored precision, halves away from zero.</summary>
    public static decimal Round(decimal amount) => Math.Round(amount, Decimals, MidpointRounding.AwayFromZero);

    /// <summary>Rounds to the stored precision, leaving a missing amount missing.</summary>
    public static decimal? Round(decimal? amount) => amount is { } value ? Round(value) : null;
}
