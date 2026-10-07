using Application.Abstractions;
using Domain.Cookbooks;
using Domain.Shared;

namespace Application.Cookbooks;

/// <summary>Whether the caller may see or change a cookbook: household members only, others are told it does not exist.</summary>
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
