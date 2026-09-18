using System.Collections.Frozen;
using Domain.Shared;

namespace Domain.Searches;

/// <summary>
/// What a saved search asks the library for.
/// </summary>
/// <remarks>
/// <para>
/// The four things the library's toolbar holds, and nothing else. It records
/// the question and never the answer — what matches is worked out whenever the
/// search is applied, so a recipe written this evening is in it immediately and
/// nothing has to be rebuilt when a filter changes.
/// </para>
/// <para>
/// The words are kept exactly as they were typed. Parsing them here would give
/// this type a second opinion about what a search means, and search already has
/// the only one worth having.
/// </para>
/// </remarks>
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
    /// <param name="query">The words in the search box, or null.</param>
    /// <param name="tags">Tag slugs a recipe must all carry.</param>
    /// <param name="maxMinutes">The longest a recipe may take, or null.</param>
    /// <param name="sort">The order to read in, or null.</param>
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
    /// <param name="query">The stored words.</param>
    /// <param name="tags">The stored tag slugs.</param>
    /// <param name="maxMinutes">The stored ceiling.</param>
    /// <param name="sort">The stored order.</param>
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

    /// <summary>
    /// Trimmed, emptied of blanks, and deduplicated.
    /// </summary>
    /// <remarks>
    /// The same tag twice is one filter written twice, and a search that
    /// reported "2 filters" for it would be counting the typing rather than the
    /// question. Ordinal, because these are slugs rather than words.
    /// </remarks>
    private static IReadOnlyList<string> Clean(IReadOnlyList<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        return
        [
            .. tags
                .Select(tag => tag.Trim())
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.Ordinal)
        ];
    }
}

/// <summary>
/// The orders a saved search may remember.
/// </summary>
/// <remarks>
/// <para>
/// The words <c>GET /recipes</c> accepts for <c>sort</c>, held here so that
/// saving an order is storing the query-string value rather than translating
/// it into something that has to be translated back.
/// </para>
/// <para>
/// <c>cookbookOrder</c> is deliberately absent: it is only legal alongside a
/// <c>cookbookId</c>, and a saved search that carried it would be one the
/// library could not apply. An integration test holds the recipe endpoint to
/// accepting everything in here, which is what stops the two drifting.
/// </para>
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
