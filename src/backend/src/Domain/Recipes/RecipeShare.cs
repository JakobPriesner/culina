namespace Domain.Recipes;

/// <summary>A recipe published behind an unguessable link.</summary>
/// <remarks>
/// No behaviour: the row's existence is the permission, and taking a link back deletes it. Unlike
/// <see cref="Households.HouseholdInvitation"/> it is not one-time or expiring: a family-chat link
/// that quietly died would be worse.
/// </remarks>
/// <param name="RecipeId">Which recipe is readable.</param>
/// <param name="Token">The secret in the link.</param>
/// <param name="CreatedBy">Who published it.</param>
/// <param name="CreatedAt">When they did.</param>
public sealed record RecipeShare(
    Guid RecipeId,
    string Token,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);
