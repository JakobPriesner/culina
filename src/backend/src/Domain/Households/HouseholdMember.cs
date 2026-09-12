namespace Domain.Households;

/// <summary>One person's membership of one household.</summary>
/// <param name="UserId">Who.</param>
/// <param name="Role">What they may do.</param>
/// <param name="JoinedAt">When they joined.</param>
public sealed record HouseholdMember(Guid UserId, HouseholdRole Role, DateTimeOffset JoinedAt);
