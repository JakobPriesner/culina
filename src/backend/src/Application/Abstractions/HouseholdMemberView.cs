namespace Application.Abstractions;

/// <summary>
/// A household member with the one thing the domain does not carry: their name.
/// </summary>
/// <remarks>
/// A read model, not an entity. <c>Household</c> holds user ids because
/// membership rules never need a name; the members screen does, and joining in
/// SQL is cheaper than loading a user per member.
/// </remarks>
/// <param name="UserId">Who.</param>
/// <param name="DisplayName">What they are called.</param>
/// <param name="Role">What they may do.</param>
/// <param name="JoinedAt">When they joined.</param>
public sealed record HouseholdMemberView(
    Guid UserId,
    string DisplayName,
    string Role,
    DateTimeOffset JoinedAt);
