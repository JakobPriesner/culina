namespace Contracts.Users.UpdatePreferences;

/// <summary>The preferences to store.</summary>
/// <remarks>
/// A complete replacement rather than a patch: there are four values, they are
/// edited on one screen, and "unchanged" is not a state worth encoding.
/// </remarks>
public sealed record Request
{
    /// <summary>The language to read in: <c>en</c> or <c>de</c>.</summary>
    public required string Locale { get; init; }

    /// <summary>The theme id.</summary>
    public required string Theme { get; init; }

    /// <summary><c>light</c>, <c>dark</c> or <c>system</c>.</summary>
    public required string Mode { get; init; }

    /// <summary><c>metric</c> or <c>imperial</c>.</summary>
    public required string MeasurementSystem { get; init; }
}
