using Application.Abstractions;
using Domain.Households;
using Domain.Shared;

namespace Application.Households;

/// <summary>
/// Points a household at the one it should inherit recipes from.
/// </summary>
/// <remarks>
/// Shared by creating a household and changing one, so a household born
/// inheriting and one told to later pass exactly the same checks.
/// </remarks>
internal static class HouseholdInheritance
{
    /// <param name="households">The household repository.</param>
    /// <param name="household">The household that inherits, changed in place.</param>
    /// <param name="parentId">Whom to inherit from, or null for nobody.</param>
    /// <param name="userId">Who is asking.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    internal static async Task<Result> ApplyAsync(
        IHouseholdRepository households,
        Household household,
        Guid? parentId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (parentId is not { } id)
        {
            return household.StopInheriting(userId);
        }

        var parent = await households.FindAsync(id, cancellationToken).ConfigureAwait(false);

        return await parent.Match(
            async found =>
            {
                var library = await HouseholdAccess
                    .LibraryAsync(households, found.Id, cancellationToken)
                    .ConfigureAwait(false);

                return household.Inherit(found, library, userId);
            },
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);
    }
}
