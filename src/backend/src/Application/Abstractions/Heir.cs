namespace Application.Abstractions;

/// <summary>
/// A household that inherits recipes from another, by name.
/// </summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="Name">What it is called.</param>
/// <param name="InheritsFrom">
/// The household it inherits from directly: the one asked about, or another
/// heir it inherits through.
/// </param>
public sealed record Heir(Guid HouseholdId, string Name, Guid InheritsFrom);
