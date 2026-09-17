using Application.Abstractions;
using Domain.Import;
using Domain.Shared;
using Npgsql;

namespace Infrastructure.Persistence.Import;

/// <summary>A <c>recipe_sources</c> row.</summary>
internal sealed record RecipeSourceRow
{
    public Guid Id { get; init; }

    public Guid HouseholdId { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public string Secret { get; init; } = string.Empty;

    public Guid CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }

    public long Version { get; init; }
}

/// <summary>Stores the libraries a household has connected.</summary>
/// <param name="executor">Runs the SQL.</param>
internal sealed class RecipeSourceRepository(DbExecutor executor) : IRecipeSourceRepository
{
    /// <summary>The index that makes connecting the same instance twice a conflict.</summary>
    private const string OnePerAddress = "recipe_sources_one_per_address_idx";

    private const string Columns =
        "id, household_id, kind, label, base_url, secret, created_by, created_at, last_used_at, version";

    public async Task<Result<RecipeSource>> FindAsync(
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<RecipeSourceRow>(
                $"select {Columns} from recipe_sources where id = @sourceId;",
                new { sourceId },
                cancellationToken)
            .ConfigureAwait(false);

        return row is null ? ImportErrors.SourceNotFound(sourceId) : row.ToDomain();
    }

    public async Task<IReadOnlyList<RecipeSource>> ListAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync<RecipeSourceRow>(
                $"""
                 select {Columns} from recipe_sources
                 where household_id = @householdId
                 order by created_at, id;
                 """,
                new { householdId },
                cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(row => row.ToDomain())];
    }

    public async Task<Result> AddAsync(RecipeSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            await executor.ExecuteAsync(
                    """
                    insert into recipe_sources
                        (id, household_id, kind, label, base_url, secret, created_by, created_at,
                         last_used_at, version)
                    values
                        (@id, @householdId, @kind, @label, @baseUrl, @secret, @createdBy, @createdAt,
                         @lastUsedAt, @version);
                    """,
                    Parameters(source),
                    cancellationToken)
                .ConfigureAwait(false);

            return Result.Success();
        }
        catch (PostgresException failure) when (failure.ConstraintName == OnePerAddress)
        {
            // Decided by the database rather than by a read before the write:
            // two people connecting the same instance at the same moment would
            // both pass a check and one would still have to lose.
            return ImportErrors.SourceAlreadyConnected;
        }
    }

    public async Task<Result> SaveAsync(RecipeSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        await executor.ExecuteAsync(
                """
                update recipe_sources
                set label = @label, secret = @secret, last_used_at = @lastUsedAt, version = @version
                where id = @id;
                """,
                Parameters(source),
                cancellationToken)
            .ConfigureAwait(false);

        // No version check. The only thing that writes here after the insert is
        // "these recipes came from you, just now" — which is a fact, not an
        // edit two people can lose each other's work over.
        return Result.Success();
    }

    public Task DeleteAsync(Guid sourceId, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "delete from recipe_sources where id = @sourceId;",
            new { sourceId },
            cancellationToken);

    private static object Parameters(RecipeSource source) => new
    {
        id = source.Id,
        householdId = source.HouseholdId,
        kind = source.Kind.Code,
        label = source.Label,
        baseUrl = source.Address.Value,
        secret = source.Secret,
        createdBy = source.CreatedBy,
        createdAt = source.CreatedAt,
        lastUsedAt = source.LastUsedAt,
        version = source.Version
    };
}

/// <summary>Rebuilds a connection from its row.</summary>
internal static class RecipeSourceRowMappings
{
    internal static RecipeSource ToDomain(this RecipeSourceRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        // A row this cannot read is a defect rather than an outcome: the check
        // constraint and the address rule are both enforced on the way in, so
        // anything here that fails them was written by something other than
        // this code.
        var kind = SourceKind.Parse(row.Kind)
            ?? throw new InvalidOperationException($"Stored source kind '{row.Kind}' is not known.");

        var address = SourceAddress.Create(row.BaseUrl).Match(
            value => value,
            error => throw new InvalidOperationException(
                $"Stored source address '{row.BaseUrl}' is not usable: {error.Code}."));

        return RecipeSource.Restore(
            row.Id,
            row.HouseholdId,
            kind,
            row.Label,
            address,
            row.Secret,
            row.CreatedBy,
            row.CreatedAt,
            row.LastUsedAt,
            row.Version);
    }
}
