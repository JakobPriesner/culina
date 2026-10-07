namespace Application.Abstractions;

/// <summary>A household whose recipes another one sees, by name: a read model with the name carried to say where a recipe comes from.</summary>
public sealed record InheritedHousehold(Guid HouseholdId, string Name);
