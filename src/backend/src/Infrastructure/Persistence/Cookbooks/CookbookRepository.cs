using Application.Abstractions;
using Domain.Cookbooks;
using Domain.Shared;

namespace Infrastructure.Persistence.Cookbooks;

/// <summary>A <c>cookbooks</c> row, with what its card draws.</summary>
internal sealed record CookbookRow
{
    public Guid Id { get; init; }

    public Guid HouseholdId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public Guid CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public long Version { get; init; }

    public string Kind { get; init; } = "manual";

    public string[] RuleTags { get; init; } = [];

    public string[] RuleIngredients { get; init; } = [];

    public int? RuleMaxMinutes { get; init; }

    public int RecipeCount { get; init; }

    public Guid[] CoverRecipeIds { get; init; } = [];

    public int TotalCount { get; init; }
}

/// <summary>Stores a household's shelves of recipes.</summary>
/// <param name="executor">Runs the SQL.</param>
internal sealed class CookbookRepository(DbExecutor executor) : ICookbookRepository
{
    /// <summary>A hard ceiling, enforced here and not only in the endpoint.</summary>
    internal const int MaxLimit = 100;

    /// <summary>
    /// How many pictures a cover shows.
    /// </summary>
    /// <remarks>
    /// Four, arranged as a mosaic — or one, or two, when that is all there is.
    /// Beyond four a cover stops being recognisable and starts being a
    /// contact sheet.
    /// </remarks>
    private const int CoverPictures = 4;

    /// <summary>
    /// Which recipes are on a shelf, whichever kind it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written once and used by both the count and the cover, because a shelf
    /// that counted one set of recipes and drew a picture of another would be
    /// wrong in a way nobody could see.
    /// </para>
    /// <para>
    /// A manual shelf names rows. A smart one names conditions, and they are
    /// the same conditions the recipe search applies — asked here rather than
    /// remembered anywhere, which is the whole of "it fills itself".
    /// </para>
    /// <para>
    /// Either way only over the shelf's household's library: its own recipes
    /// and the ones it inherits. A recipe that stops being inherited drops off
    /// the shelves it was put on rather than lingering there unopenable, and
    /// comes back if the inheritance does.
    /// </para>
    /// </remarks>
    private static readonly string OnTheShelf = $"""
        select r.id, r.image_id, cr.added_at
        from recipes r
        left join cookbook_recipes cr
               on cr.cookbook_id = c.id and cr.recipe_id = r.id
        where r.household_id = any(array(select household_library(c.household_id)))
          and (
            case when c.kind = 'manual' then cr.recipe_id is not null
            else
                {SmartShelfSql.Matches("c.rule_tags", "c.rule_ingredients", "c.rule_max_minutes")}
            end)
        """;

    /// <summary>
    /// The count and the cover, read with the shelf rather than after it.
    /// </summary>
    /// <remarks>
    /// Correlated subqueries and not a join with a group by: a shelf with
    /// nothing on it must still come back, and the count and the pictures are
    /// two different slices of the same set. Oldest first, so a cover stops
    /// moving once four photographed recipes are on it — a face that changed
    /// every time something was added is not one anybody would learn. A smart
    /// shelf has no added_at, so its cover falls back to the recipe id, which
    /// is time-ordered anyway.
    /// </remarks>
    private static readonly string Shelf = $$"""
        select c.id, c.household_id, c.name, c.description, c.created_by,
               c.created_at, c.updated_at, c.version,
               c.kind, c.rule_tags, c.rule_ingredients, c.rule_max_minutes,
               (select count(*) from ({{OnTheShelf}}) as counted) as recipe_count,
               coalesce(
                   array(
                       select pictured.id from ({{OnTheShelf}}) as pictured
                       where pictured.image_id is not null
                       order by pictured.added_at nulls last, pictured.id
                       limit @coverPictures),
                   '{}') as cover_recipe_ids
        from cookbooks c
        """;

    public async Task<Result<Cookbook>> FindAsync(Guid cookbookId, CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<CookbookRow>(
            """
            select id, household_id, name, description, created_by, created_at, updated_at, version,
                   kind, rule_tags, rule_ingredients, rule_max_minutes
            from cookbooks
            where id = @cookbookId;
            """,
            new { cookbookId },
            cancellationToken).ConfigureAwait(false);

        return row is null ? CookbookErrors.NotFound(cookbookId) : ToCookbook(row);
    }

    public async Task<Result<CookbookOnAShelf>> DescribeAsync(
        Guid cookbookId,
        CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<CookbookRow>(
            $"{Shelf} where c.id = @cookbookId;",
            new { cookbookId, coverPictures = CoverPictures },
            cancellationToken).ConfigureAwait(false);

        return row is null ? CookbookErrors.NotFound(cookbookId) : ToShelf(row);
    }

    public async Task<CookbookPage> ListAsync(
        Guid householdId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var size = Math.Clamp(limit, 1, MaxLimit);
        var resume = CookbookCursor.Decode(cursor);

        // One extra row tells us whether there is a next page without a second
        // query, and the total comes from the same pass so a page and its count
        // can never disagree.
        var rows = await executor.QueryAsync<CookbookRow>(
            $"""
            with shelves as (
                {Shelf} where c.household_id = @householdId),
            counted as (select *, count(*) over () as total_count from shelves)
            select * from counted
            {(resume is null ? string.Empty : "where (updated_at, id) < (@cursorUpdatedAt, @cursorId)")}
            order by updated_at desc, id desc
            limit {size + 1};
            """,
            new
            {
                householdId,
                coverPictures = CoverPictures,
                cursorUpdatedAt = resume?.UpdatedAt,
                cursorId = resume?.Id
            },
            cancellationToken).ConfigureAwait(false);

        var page = rows.Take(size).Select(ToShelf).ToList();
        var last = rows.Count > size ? page[^1] : null;

        return new CookbookPage(
            page,
            last is null ? null : new CookbookCursor(last.Cookbook.UpdatedAt, last.Cookbook.Id).Encode(),
            rows.Count == 0 ? 0 : rows[0].TotalCount);
    }

    public async Task<Result> AddAsync(Cookbook cookbook, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cookbook);

        await executor.ExecuteAsync(
            """
            insert into cookbooks
                (id, household_id, name, description, kind,
                 rule_tags, rule_ingredients, rule_max_minutes,
                 created_by, created_at, updated_at, version)
            values (@id, @householdId, @name, @description, @kind,
                    @ruleTags, @ruleIngredients, @ruleMaxMinutes,
                    @createdBy, @createdAt, @updatedAt, 1);
            """,
            new
            {
                id = cookbook.Id,
                householdId = cookbook.HouseholdId,
                name = cookbook.Name.Value,
                description = cookbook.Description,
                kind = CookbookCodes.Of(cookbook.Kind),
                ruleTags = cookbook.Rules.Tags.ToArray(),
                ruleIngredients = cookbook.Rules.Ingredients.ToArray(),
                ruleMaxMinutes = cookbook.Rules.MaxMinutes,
                createdBy = cookbook.CreatedBy,
                createdAt = cookbook.CreatedAt,
                updatedAt = cookbook.UpdatedAt
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<long>> SaveAsync(
        Cookbook cookbook,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cookbook);

        // The version is in the WHERE, never compared in C#: zero rows means
        // somebody else wrote first, which is the one answer a read-then-write
        // check cannot give reliably.
        var version = await executor.QuerySingleOrDefaultAsync<long?>(
            """
            update cookbooks
            set name = @name, description = @description, updated_at = @updatedAt,
                rule_tags = @ruleTags, rule_ingredients = @ruleIngredients,
                rule_max_minutes = @ruleMaxMinutes,
                version = version + 1
            where id = @id and version = @expectedVersion
            returning version;
            """,
            new
            {
                id = cookbook.Id,
                name = cookbook.Name.Value,
                description = cookbook.Description,
                updatedAt = cookbook.UpdatedAt,
                ruleTags = cookbook.Rules.Tags.ToArray(),
                ruleIngredients = cookbook.Rules.Ingredients.ToArray(),
                ruleMaxMinutes = cookbook.Rules.MaxMinutes,
                expectedVersion
            },
            cancellationToken).ConfigureAwait(false);

        return version is null ? ConcurrencyErrors.VersionMismatch : version.Value;
    }

    public Task DeleteAsync(Guid cookbookId, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            "delete from cookbooks where id = @cookbookId;",
            new { cookbookId },
            cancellationToken);

    public async Task<bool> AddRecipeAsync(
        Guid cookbookId,
        Guid recipeId,
        Guid addedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // do nothing, not do update: a recipe already on the shelf keeps the
        // moment it went on. "Added in March" should not become "added today"
        // because somebody tapped the button twice.
        var written = await executor.ExecuteAsync(
            """
            insert into cookbook_recipes (cookbook_id, recipe_id, added_at, added_by)
            values (@cookbookId, @recipeId, @now, @addedBy)
            on conflict (cookbook_id, recipe_id) do nothing;
            """,
            new { cookbookId, recipeId, now, addedBy },
            cancellationToken).ConfigureAwait(false);

        return written > 0;
    }

    public async Task<bool> RemoveRecipeAsync(
        Guid cookbookId,
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        var removed = await executor.ExecuteAsync(
            "delete from cookbook_recipes where cookbook_id = @cookbookId and recipe_id = @recipeId;",
            new { cookbookId, recipeId },
            cancellationToken).ConfigureAwait(false);

        return removed > 0;
    }

    public Task TouchAsync(Guid cookbookId, DateTimeOffset now, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            """
            update cookbooks set updated_at = @now, version = version + 1
            where id = @cookbookId;
            """,
            new { cookbookId, now },
            cancellationToken);

    public async Task<IReadOnlyList<Guid>> RecipeIdsAsync(
        Guid cookbookId,
        CancellationToken cancellationToken)
    {
        // Ids and nothing else: this answers "is it already on?" for every row
        // of a picker at once, and a shelf of a thousand is a few kilobytes.
        var ids = await executor.QueryAsync<Guid>(
            $"""
            select on_shelf.id
            from cookbooks c
            cross join lateral ({OnTheShelf}) as on_shelf
            where c.id = @cookbookId;
            """,
            new { cookbookId },
            cancellationToken).ConfigureAwait(false);

        return [.. ids];
    }

    public async Task<IReadOnlyList<CookbookOnAShelf>> ContainingAsync(
        Guid recipeId,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        // Only the shelf's own row. This answers the tick marks in a sheet and
        // the line under a recipe's title, neither of which draws a cover.
        //
        // Both kinds, through the same fragment the count and the cover use: a
        // recipe is as genuinely on a shelf that matched it as on one somebody
        // put it on, and a recipe page that admitted only the second would be
        // the one place in the app where a smart cookbook was invisible.
        var rows = await executor.QueryAsync<CookbookRow>(
            $"""
            select c.id, c.household_id, c.name, c.description, c.created_by,
                   c.created_at, c.updated_at, c.version,
                   c.kind, c.rule_tags, c.rule_ingredients, c.rule_max_minutes
            from cookbooks c
            where c.household_id = @householdId
              and exists (
                  select 1 from ({OnTheShelf}) as on_shelf where on_shelf.id = @recipeId)
            order by c.name, c.id;
            """,
            new { recipeId, householdId },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => new CookbookOnAShelf(ToCookbook(row), row.RecipeCount, row.CoverRecipeIds))];
    }

    private static CookbookOnAShelf ToShelf(CookbookRow row) =>
        new(ToCookbook(row), row.RecipeCount, row.CoverRecipeIds);

    private static Cookbook ToCookbook(CookbookRow row) =>
        Cookbook.Restore(
            row.Id,
            row.HouseholdId,
            Unwrap(CookbookName.Create(row.Name)),
            row.Description,
            CookbookCodes.ToKind(row.Kind),
            CookbookRules.Restore(row.RuleTags, row.RuleIngredients, row.RuleMaxMinutes),
            row.CreatedBy,
            row.CreatedAt,
            row.UpdatedAt,
            row.Version);

    /// <summary>
    /// The name as stored.
    /// </summary>
    /// <remarks>
    /// It went through the value object on the way in, so a row carrying one it
    /// would now reject is a corrupt row — a defect, not a bad request, and
    /// nothing in this call stack could act on it as a failure.
    /// </remarks>
    private static CookbookName Unwrap(Result<CookbookName> result) =>
        result.Match(
            name => name,
            error => throw new InvalidOperationException(
                $"Stored cookbook name is not valid ({error.Code}). The row is corrupt."));
}

/// <summary>How a cookbook's kind is spelled in the database.</summary>
/// <remarks>
/// Text rather than an integer, for the reason the meal plan's slot is: a
/// migration that inserted a kind in the middle could not silently reassign
/// every row, and somebody reading the table can see what it says.
/// </remarks>
internal static class CookbookCodes
{
    internal static string Of(CookbookKind kind) => kind == CookbookKind.Smart ? "smart" : "manual";

    internal static CookbookKind ToKind(string stored) =>
        stored == "smart" ? CookbookKind.Smart : CookbookKind.Manual;
}
