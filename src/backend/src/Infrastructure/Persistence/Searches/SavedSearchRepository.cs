using Application.Abstractions;
using Domain.Searches;
using Domain.Shared;
using Npgsql;

namespace Infrastructure.Persistence.Searches;

/// <summary>A <c>saved_searches</c> row.</summary>
internal sealed record SavedSearchRow
{
    public Guid Id { get; init; }

    public Guid HouseholdId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Query { get; init; }

    public string[] Tags { get; init; } = [];

    public int? MaxMinutes { get; init; }

    public string? Sort { get; init; }

    public Guid CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Stores a household's saved searches.</summary>
/// <param name="executor">Runs the SQL.</param>
internal sealed class SavedSearchRepository(DbExecutor executor) : ISavedSearchRepository
{
    /// <summary>Named so a future rename fails loudly rather than turning a conflict into a 500.</summary>
    private const string NamedOnce = "saved_searches_named_once_idx";

    private const string Columns =
        "id, household_id, name, query, tags, max_minutes, sort, created_by, created_at, updated_at";

    public async Task<Result<SavedSearch>> FindAsync(
        Guid searchId,
        CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<SavedSearchRow>(
            $"select {Columns} from saved_searches where id = @searchId;",
            new { searchId },
            cancellationToken).ConfigureAwait(false);

        return row is null ? SavedSearchErrors.NotFound(searchId) : ToSearch(row);
    }

    public async Task<IReadOnlyList<SavedSearch>> ListAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync<SavedSearchRow>(
            $"""
            select {Columns} from saved_searches
            where household_id = @householdId
            order by created_at, id;
            """,
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(ToSearch)];
    }

    public async Task<Result> AddAsync(SavedSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        try
        {
            await executor.ExecuteAsync(
                """
                insert into saved_searches
                    (id, household_id, name, query, tags, max_minutes, sort,
                     created_by, created_at, updated_at)
                values (@id, @householdId, @name, @query, @tags, @maxMinutes, @sort,
                        @createdBy, @createdAt, @updatedAt);
                """,
                Parameters(search),
                cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (PostgresException failure) when (failure.ConstraintName == NamedOnce)
        {
            // The sanctioned exception to "never catch to return a failure":
            // two people can save a search by the same name between a check and
            // this insert, and the unique index is the only place that can
            // settle the race.
            return SavedSearchErrors.NameTaken;
        }
    }

    public async Task<Result> SaveAsync(SavedSearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);

        try
        {
            var rows = await executor.ExecuteAsync(
                """
                update saved_searches
                set name = @name, query = @query, tags = @tags,
                    max_minutes = @maxMinutes, sort = @sort, updated_at = @updatedAt
                where id = @id;
                """,
                Parameters(search),
                cancellationToken).ConfigureAwait(false);

            // Deleted between the read and the write, which is the same answer
            // as never having existed.
            return rows == 0 ? SavedSearchErrors.NotFound(search.Id) : Result.Success();
        }
        catch (PostgresException failure) when (failure.ConstraintName == NamedOnce)
        {
            return SavedSearchErrors.NameTaken;
        }
    }

    public Task DeleteAsync(Guid searchId, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "delete from saved_searches where id = @searchId;",
            new { searchId },
            cancellationToken);

    private static object Parameters(SavedSearch search) => new
    {
        id = search.Id,
        householdId = search.HouseholdId,
        name = search.Name.Value,
        query = search.Criteria.Query,
        tags = search.Criteria.Tags.ToArray(),
        maxMinutes = search.Criteria.MaxMinutes,
        sort = search.Criteria.Sort,
        createdBy = search.CreatedBy,
        createdAt = search.CreatedAt,
        updatedAt = search.UpdatedAt
    };

    private static SavedSearch ToSearch(SavedSearchRow row) =>
        SavedSearch.Restore(
            row.Id,
            row.HouseholdId,
            Unwrap(SavedSearchName.Create(row.Name)),
            SearchCriteria.Restore(row.Query, row.Tags, row.MaxMinutes, row.Sort),
            row.CreatedBy,
            row.CreatedAt,
            row.UpdatedAt);

    /// <summary>
    /// The name as stored.
    /// </summary>
    /// <remarks>
    /// It went through the value object on the way in, so a row carrying one it
    /// would now reject is a corrupt row — a defect, not a bad request.
    /// </remarks>
    private static SavedSearchName Unwrap(Result<SavedSearchName> result) =>
        result.Match(
            name => name,
            error => throw new InvalidOperationException(
                $"Stored saved-search name is not valid ({error.Code}). The row is corrupt."));
}
