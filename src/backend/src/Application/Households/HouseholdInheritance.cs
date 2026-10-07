using Application.Abstractions;
using Domain.Households;
using Domain.Shared;

namespace Application.Households;

/// <summary>Points a household at the one it should inherit recipes from; shared by creating and changing so both pass the same checks.</summary>
internal static class HouseholdInheritance
{
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
