using Application.Abstractions;
using Domain.Recipes;
using Domain.Shared;

namespace Infrastructure.Persistence.Recipes;

/// <summary>The <c>recipe_shares</c> row as PostgreSQL returns it.</summary>
internal sealed record RecipeShareRow
{
    public Guid RecipeId { get; init; }

    public string Token { get; init; } = string.Empty;

    public Guid CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Stores the links that publish a recipe.</summary>
/// <param name="executor">Runs the SQL.</param>
internal sealed class RecipeShareRepository(DbExecutor executor) : IRecipeShareRepository
{
    private const string Columns = "recipe_id, token, created_by, created_at";

    public async Task<Result<RecipeShare>> FindAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<RecipeShareRow>(
            $"select {Columns} from recipe_shares where recipe_id = @recipeId;",
            new { recipeId },
            cancellationToken).ConfigureAwait(false);

        return row is null ? RecipeErrors.ShareNotFound : row.ToDomain();
    }

    public async Task<Result<RecipeShare>> FindByTokenAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<RecipeShareRow>(
            $"select {Columns} from recipe_shares where token = @token;",
            new { token },
            cancellationToken).ConfigureAwait(false);

        return row is null ? RecipeErrors.ShareNotFound : row.ToDomain();
    }

    public async Task<Result<RecipeShare>> AddOrKeepAsync(
        RecipeShare share,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(share);

        // An update that changes nothing, not `do nothing`: `returning` then hands back the row that
        // won, even when it was committed after this statement began. A select of it in the same
        // statement could not see it, so a concurrent caller would get no link at all.
        var row = await executor.QuerySingleOrDefaultAsync<RecipeShareRow>(
            $"""
             insert into recipe_shares (recipe_id, token, created_by, created_at)
             values (@recipeId, @token, @createdBy, @createdAt)
             on conflict (recipe_id) do update set token = recipe_shares.token
             returning {Columns};
             """,
            new
            {
                recipeId = share.RecipeId,
                token = share.Token,
                createdBy = share.CreatedBy,
                createdAt = share.CreatedAt
            },
            cancellationToken).ConfigureAwait(false);

        return row is null ? RecipeErrors.ShareNotFound : row.ToDomain();
    }

    public async Task<Result> RemoveAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        // Deleting nothing is success: "not shared" is the requested state.
        await executor.ExecuteAsync(
            "delete from recipe_shares where recipe_id = @recipeId;",
            new { recipeId },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>Turns a stored row back into a domain share.</summary>
internal static class RecipeShareRowMappings
{
    internal static RecipeShare ToDomain(this RecipeShareRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new RecipeShare(row.RecipeId, row.Token, row.CreatedBy, row.CreatedAt);
    }
}
