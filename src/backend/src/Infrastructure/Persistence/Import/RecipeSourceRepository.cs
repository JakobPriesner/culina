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
/// <param name="tokens">
/// Encrypts each connection's API token on the way in and decrypts it on the
/// way out, under a purpose of its own, so a database dump holds no working
/// credential for anybody's other app.
/// </param>
internal sealed class RecipeSourceRepository(
    DbExecutor executor,
    [FromKeyedServices(SecretProtector.SourceTokens)] ISecretProtector tokens) : IRecipeSourceRepository
{
    /// <summary>The index that makes connecting the same instance twice a conflict.</summary>
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

        // A connection is only ever made with a token in hand; one without is
        // something read back from storage, and that is never inserted again.
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
            // Decided by the database rather than by a read before the write:
            // two people connecting the same instance at the same moment would
            // both pass a check and one would still have to lose.
            return ImportErrors.SourceAlreadyConnected;
        }
    }

    public async Task<Result> SaveAsync(RecipeSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        // The token is not written here. It never changes after a connection is
        // made — connecting again is a new row — and leaving it alone means a
        // save can never overwrite a token with one it failed to read.
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
}

/// <summary>Rebuilds a connection from its row.</summary>
internal static class RecipeSourceRowMappings
{
    internal static RecipeSource ToDomain(this RecipeSourceRow row, ISecretProtector tokens)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(tokens);

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

        // An unreadable token is the exception to that: it is an outcome, and
        // an ordinary one — the key ring was lost and restored empty. The
        // connection comes back without a token and asks to be made again,
        // rather than failing every screen that lists it. A plain one is a row
        // written before tokens were encrypted, between the migration that
        // marked it and the startup step that encrypts it.
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
