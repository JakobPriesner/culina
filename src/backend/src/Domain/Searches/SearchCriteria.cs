using System.Collections.Frozen;
using Domain.Shared;

namespace Domain.Searches;

/// <summary>What a saved search asks the library for: the question, never the answer, with words kept as typed.</summary>
public sealed record SearchCriteria
{
    /// <summary>More tags than anybody would narrow by at once.</summary>
    public const int MaxTags = 20;

    /// <summary>The longest tag slug a filter may name.</summary>
    public const int MaxTagLength = 120;

    /// <summary>As long as the search box itself accepts.</summary>
    public const int MaxQueryLength = 200;

    /// <summary>A week, in minutes.</summary>
    public const int MaxMinutesCeiling = 10_080;

    private SearchCriteria(string? query, IReadOnlyList<string> tags, int? maxMinutes, string? sort)
    {
        Query = query;
        Tags = tags;
        MaxMinutes = maxMinutes;
        Sort = sort;
    }

    /// <summary>The words that were in the search box, or null.</summary>
    public string? Query { get; }

    /// <summary>Tag slugs a recipe must all carry.</summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>The longest a recipe may take, or null for any length.</summary>
    public int? MaxMinutes { get; }

    /// <summary>The order it was read in, or null for whatever the library would choose.</summary>
    public string? Sort { get; }

    /// <summary>Whether this asks for nothing at all, which is the whole library.</summary>
    public bool Empty => Query is null && Tags.Count == 0 && MaxMinutes is null && Sort is null;

    /// <summary>Parses a set of filters, returning a failure rather than throwing.</summary>
    public static Result<SearchCriteria> Create(
        string? query,
        IReadOnlyList<string>? tags,
        int? maxMinutes,
        string? sort)
    {
        var words = Normalise(query);
        var cleaned = Clean(tags);
        var order = Normalise(sort);

        if (words is { Length: > MaxQueryLength } || cleaned.Count > MaxTags)
        {
            return SavedSearchErrors.InvalidCriteria;
        }

        if (cleaned.Any(tag => tag.Length > MaxTagLength))
        {
            return SavedSearchErrors.InvalidCriteria;
        }

        if (maxMinutes is <= 0 or > MaxMinutesCeiling)
        {
            return SavedSearchErrors.InvalidCriteria;
        }

        if (order is not null && !SearchOrders.Known.Contains(order))
        {
            return SavedSearchErrors.InvalidCriteria;
        }

        var criteria = new SearchCriteria(words, cleaned, maxMinutes, order);

        return criteria.Empty ? SavedSearchErrors.CriteriaRequired : criteria;
    }

    /// <summary>Rebuilds filters from storage.</summary>
    public static SearchCriteria Restore(
        string? query,
        IReadOnlyList<string> tags,
        int? maxMinutes,
        string? sort) =>
        new(query, tags, maxMinutes, sort);

    /// <summary>An empty string and nothing typed are the same thing.</summary>
    private static string? Normalise(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    // The same tag twice is one filter. Ordinal, because these are slugs.
    private static IReadOnlyList<string> Clean(IReadOnlyList<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        return
        [
            .. tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.Ordinal)
        ];
    }
}

/// <summary>The orders a saved search may remember: the <c>sort</c> values <c>GET /recipes</c> accepts.</summary>
/// <remarks>
/// <c>cookbookOrder</c> is absent as it is only legal with a <c>cookbookId</c>. An integration test holds the
/// endpoint to accepting everything here.
/// </remarks>
public static class SearchOrders
{
    /// <summary>Every order a saved search may name.</summary>
    public static readonly FrozenSet<string> Known = new[]
    {
        "relevance",
        "suggested",
        "-updatedAt",
        "title",
        "totalMinutes",
        "-cookCount"
    }.ToFrozenSet(StringComparer.Ordinal);
}
