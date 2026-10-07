using Application.Abstractions;
using Domain.Import;
using Domain.Shared;
using Infrastructure.Assistance;
using Microsoft.Extensions.DependencyInjection;
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

    public bool SecretProtected { get; init; }

    public Guid CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastUsedAt { get; init; }

    public long Version { get; init; }
}

/// <summary>Stores the libraries a household has connected.</summary>
/// <param name="executor">Runs the SQL.</param>
/// <param name="tokens">Encrypts each connection's API token under its own purpose, so a database dump holds no working credential.</param>
internal sealed class RecipeSourceRepository(
    DbExecutor executor,
    [FromKeyedServices(SecretProtector.SourceTokens)] ISecretProtector tokens) : IRecipeSourceRepository
{
    private const string OnePerAddress = "recipe_sources_one_per_address_idx";

    private const string Columns =
        "id, household_id, kind, label, base_url, secret, secret_protected, created_by, created_at, last_used_at, version";

    public async Task<Result<RecipeSource>> FindAsync(
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<RecipeSourceRow>(
                $"select {Columns} from recipe_sources where id = @sourceId;",
                new { sourceId },
                cancellationToken)
            .ConfigureAwait(false);

        return row is null ? ImportErrors.SourceNotFound(sourceId) : row.ToDomain(tokens);
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

        return [.. rows.Select(row => row.ToDomain(tokens))];
    }

    public async Task<Result> AddAsync(RecipeSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        // A source read back from storage has no token and is never inserted again.
        var secret = source.Secret
            ?? throw new InvalidOperationException("A connection is added with the token it was made with.");

        try
        {
            await executor.ExecuteAsync(
                    """
                    insert into recipe_sources
                        (id, household_id, kind, label, base_url, secret, secret_protected, created_by,
                         created_at, last_used_at, version)
                    values
                        (@id, @householdId, @kind, @label, @baseUrl, @secret, true, @createdBy,
                         @createdAt, @lastUsedAt, @version);
                    """,
                    new
                    {
                        id = source.Id,
                        householdId = source.HouseholdId,
                        kind = source.Kind.Code,
                        label = source.Label,
                        baseUrl = source.Address.Value,
                        secret = tokens.Protect(secret),
                        createdBy = source.CreatedBy,
                        createdAt = source.CreatedAt,
                        lastUsedAt = source.LastUsedAt,
                        version = source.Version
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return Result.Success();
        }
        catch (PostgresException failure) when (failure.ConstraintName == OnePerAddress)
        {
            // Decided by the index, not a prior read, which two concurrent connects would both pass.
            return ImportErrors.SourceAlreadyConnected;
        }
    }

    public async Task<Result> SaveAsync(RecipeSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        // The token is never rewritten, so a save cannot overwrite it with one that failed to read.
        await executor.ExecuteAsync(
                """
                update recipe_sources
                set label = @label, last_used_at = @lastUsedAt, version = @version
                where id = @id;
                """,
                new
                {
                    id = source.Id,
                    label = source.Label,
                    lastUsedAt = source.LastUsedAt,
                    version = source.Version
                },
                cancellationToken)
            .ConfigureAwait(false);

        // No version check: the only later write is "used just now", not an edit anyone can lose.
        return Result.Success();
    }

    public Task DeleteAsync(Guid sourceId, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "delete from recipe_sources where id = @sourceId;",
            new { sourceId },
            cancellationToken);
}

/// <summary>Rebuilds a connection from its row.</summary>
internal static class RecipeSourceRowMappings
{
    internal static RecipeSource ToDomain(this RecipeSourceRow row, ISecretProtector tokens)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(tokens);

        // Kind and address are enforced on the way in, so a row failing them is a defect.
        var kind = SourceKind.Parse(row.Kind)
            ?? throw new InvalidOperationException($"Stored source kind '{row.Kind}' is not known.");

        var address = SourceAddress.Create(row.BaseUrl).Match(
            value => value,
            error => throw new InvalidOperationException(
                $"Stored source address '{row.BaseUrl}' is not usable: {error.Code}."));

        // An unreadable token (lost key ring) is an ordinary outcome: the connection comes back without
        // one and asks to be remade. An unprotected one predates token encryption.
        var secret = row.SecretProtected ? tokens.Unprotect(row.Secret) : row.Secret;

        return RecipeSource.Restore(
            row.Id,
            row.HouseholdId,
            kind,
            row.Label,
            address,
            secret,
            row.CreatedBy,
            row.CreatedAt,
            row.LastUsedAt,
            row.Version);
    }
}
