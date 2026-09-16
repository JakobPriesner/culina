using Application.Abstractions;
using Domain.Cookbooks;
using Domain.Shared;

namespace Application.Cookbooks;

/// <summary>
/// Answers whether the caller may see or change a cookbook.
/// </summary>
/// <remarks>
/// The same rule recipes follow, for the same reason: a cookbook belongs to a
/// household, so the question is only "are you in it" — and somebody who is not
/// is told the cookbook does not exist, never that it exists and is forbidden.
/// </remarks>
internal static class CookbookAccess
{
    internal static async Task<Result<CookbookOnAShelf>> VisibleAsync(
        ICookbookRepository cookbooks,
        IHouseholdRepository households,
        Guid cookbookId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var found = await cookbooks.DescribeAsync(cookbookId, cancellationToken).ConfigureAwait(false);

        return await found.Match(
            async shelf => await households
                .IsMemberAsync(shelf.Cookbook.HouseholdId, userId, cancellationToken)
                .ConfigureAwait(false)
                    ? Result<CookbookOnAShelf>.Success(shelf)
                    : CookbookErrors.NotFound(cookbookId),
            error => Task.FromResult(Result<CookbookOnAShelf>.Failure(error))).ConfigureAwait(false);
    }
}
