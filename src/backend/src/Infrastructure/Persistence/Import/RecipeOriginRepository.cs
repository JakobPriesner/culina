using Application.Abstractions;
using Domain.Import;
using Domain.Shared;
using Npgsql;

namespace Infrastructure.Persistence.Import;

/// <summary>A <c>recipe_origins</c> row.</summary>
internal sealed record RecipeOriginRow
{
    public Guid RecipeId { get; init; }

    public Guid HouseholdId { get; init; }

    public string Kind { get; init; } = string.Empty;

    public Guid? SourceId { get; init; }

    public string ExternalId { get; init; } = string.Empty;

    public string? SourceUrl { get; init; }

    public DateTimeOffset ImportedAt { get; init; }
}

/// <summary>Stores where imported recipes came from.</summary>
/// <param name="executor">Runs the SQL.</param>
internal sealed class RecipeOriginRepository(DbExecutor executor) : IRecipeOriginRepository
{
    /// <summary>The index that makes importing the same recipe twice a conflict.</summary>
    private const string OncePerHousehold = "recipe_origins_once_per_household_idx";

    private const string Columns =
        "recipe_id, household_id, kind, source_id, external_id, source_url, imported_at";

    public async Task<Result> AddAsync(RecipeOrigin origin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(origin);

        try
        {
            await executor.ExecuteAsync(
                    """
                    insert into recipe_origins
                        (recipe_id, household_id, kind, source_id, external_id, source_url, imported_at)
                    values
                        (@recipeId, @householdId, @kind, @sourceId, @externalId, @sourceUrl, @importedAt);
                    """,
                    new
                    {
                        recipeId = origin.RecipeId,
                        householdId = origin.HouseholdId,
                        kind = origin.Kind.Code,
                        sourceId = origin.SourceId,
                        externalId = origin.ExternalId,
                        sourceUrl = origin.SourceUrl,
                        importedAt = origin.ImportedAt
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return Result.Success();
        }
        catch (PostgresException failure) when (failure.ConstraintName == OncePerHousehold)
        {
            // Two imports of the same recipe racing each other. The loser rolls
            // back its recipe with the transaction it is inside, which is
            // exactly right: the winner's copy is already there.
            return ImportErrors.AlreadyImported;
        }
    }

    public async Task<Result<RecipeOrigin>> FindAsync(
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<RecipeOriginRow>(
                $"select {Columns} from recipe_origins where recipe_id = @recipeId;",
                new { recipeId },
                cancellationToken)
            .ConfigureAwait(false);

        return row is null
            // Not a failure worth a message: most recipes were written here,
            // and having no origin is the ordinary case.
            ? ImportErrors.NoOrigin
            : row.ToDomain();
    }

    public async Task<IReadOnlyDictionary<string, Guid>> AlreadyHereAsync(
        Guid householdId,
        SourceKind kind,
        IReadOnlyList<string> externalIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(externalIds);

        if (externalIds.Count == 0)
        {
            return new Dictionary<string, Guid>(StringComparer.Ordinal);
        }

        var rows = await executor.QueryAsync<RecipeOriginRow>(
                """
                select recipe_id, external_id from recipe_origins
                where household_id = @householdId
                  and kind = @kind
                  and external_id = any(@externalIds);
                """,
                new { householdId, kind = kind.Code, externalIds = externalIds.ToArray() },
                cancellationToken)
            .ConfigureAwait(false);

        // Last one wins on a duplicate, which cannot happen: the unique index
        // is on exactly this triple.
        return rows.ToDictionary(row => row.ExternalId, row => row.RecipeId, StringComparer.Ordinal);
    }
}

/// <summary>Rebuilds provenance from its row.</summary>
internal static class RecipeOriginRowMappings
{
    internal static RecipeOrigin ToDomain(this RecipeOriginRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var kind = SourceKind.Parse(row.Kind)
            ?? throw new InvalidOperationException($"Stored origin kind '{row.Kind}' is not known.");

        return new RecipeOrigin(
            row.RecipeId,
            row.HouseholdId,
            kind,
            row.SourceId,
            row.ExternalId,
            row.SourceUrl,
            row.ImportedAt);
    }
}
