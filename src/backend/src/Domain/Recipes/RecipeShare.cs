namespace Domain.Recipes;

/// <summary>
/// A recipe that has been published behind an unguessable link.
/// </summary>
/// <remarks>
/// <para>
/// No behaviour, because there is none to have: the row's existence is the
/// permission. There is nothing to expire, nothing to redeem and no second
/// state — taking a link back deletes it, which is the only rule this thing
/// has and the database enforces it without help.
/// </para>
/// <para>
/// Deliberately unlike <see cref="Households.HouseholdInvitation"/>, which is
/// one-time and expiring. That one hands over a whole kitchen for good; this
/// one hands over one recipe to read, and a link in a family chat that quietly
/// stopped working after a fortnight is a worse outcome than the one it would
/// be protecting against.
/// </para>
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
