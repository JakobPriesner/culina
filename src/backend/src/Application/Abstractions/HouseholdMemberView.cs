namespace Application.Abstractions;

/// <summary>A household member plus their name, which the domain does not carry. A read model joined in SQL.</summary>
/// <param name="UserId">Who.</param>
/// <param name="DisplayName">What they are called.</param>
/// <param name="Role">What they may do.</param>
/// <param name="JoinedAt">When they joined.</param>
public sealed record HouseholdMemberView(
    Guid UserId,
    string DisplayName,
    string Role,
    DateTimeOffset JoinedAt);
