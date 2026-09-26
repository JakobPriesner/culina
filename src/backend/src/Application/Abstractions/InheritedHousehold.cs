namespace Application.Abstractions;

/// <summary>
/// A household whose recipes another one sees, by name.
/// </summary>
/// <remarks>
/// A read model, like <see cref="HouseholdMemberView"/>: the chain is walked in
/// SQL, and the name is carried because the whole point of it is saying where a
/// recipe comes from.
/// </remarks>
/// <param name="HouseholdId">Which household.</param>
/// <param name="Name">What it is called.</param>
public sealed record InheritedHousehold(Guid HouseholdId, string Name);
