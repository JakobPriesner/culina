using Contracts.Searches;
using Domain.Searches;
using Domain.Shared;

namespace Application.Searches;

/// <summary>Turns saved searches into what the wire carries, and back.</summary>
internal static class SavedSearchMappings
{
    internal static SavedSearchesResponse ToResponse(this IReadOnlyList<SavedSearch> searches) =>
        new() { Items = [.. searches.Select(ToDetail)] };

    internal static SavedSearchDetail ToDetail(this SavedSearch search) => new()
    {
        SearchId = search.Id,
        HouseholdId = search.HouseholdId,
        Name = search.Name.Value,
        Criteria = new SearchCriteriaContract
        {
            Query = search.Criteria.Query,
            Tags = search.Criteria.Tags,
            MaxMinutes = search.Criteria.MaxMinutes,
            Sort = search.Criteria.Sort
        },
        CreatedBy = search.CreatedBy,
        CreatedAt = search.CreatedAt,
        UpdatedAt = search.UpdatedAt
    };

    /// <summary>Reads the filters a request states.</summary>
    /// <param name="wire">What the caller sent.</param>
    internal static Result<SearchCriteria> ToCriteria(SearchCriteriaContract? wire) =>
        wire is null
            ? SavedSearchErrors.CriteriaRequired
            : SearchCriteria.Create(wire.Query, wire.Tags, wire.MaxMinutes, wire.Sort);
}
