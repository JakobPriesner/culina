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

    public Guid[] CoverImageIds { get; init; } = [];

    public int TotalCount { get; init; }
}

/// <summary>Stores a household's shelves of recipes.</summary>
internal sealed class CookbookRepository(DbExecutor executor) : ICookbookRepository
{
    /// <summary>A hard ceiling, enforced here and not only in the endpoint.</summary>
    internal const int MaxLimit = 100;

    // Four pictures as a mosaic; beyond that a cover becomes a contact sheet.
    private const int CoverPictures = 4;

    // Shared by the count and the cover so they cannot disagree. A smart shelf applies the same conditions
    // as recipe search, only over the household's own and inherited recipes, so a recipe that stops being
    // inherited drops off and returns with the inheritance.
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

    private const string ShelfColumns = """
        c.id, c.household_id, c.name, c.description, c.created_by,
        c.created_at, c.updated_at, c.version,
        c.kind, c.rule_tags, c.rule_ingredients, c.rule_max_minutes
        """;

    /// <summary>The count and cover, read with the shelf.</summary>
    /// <remarks>
    /// Lateral so the shelf's set is computed once (a smart shelf scans the library) and an empty shelf still comes back.
    /// Oldest first so the cover stops moving once full; smart shelves have no added_at, so they fall back to the time-ordered id.
    /// </remarks>
    private static readonly string ShelfContents = $$"""
        cross join lateral (
            select count(*) as recipe_count,
                   coalesce(
                       (array_agg(on_shelf.id order by on_shelf.added_at nulls last, on_shelf.id)
                            filter (where on_shelf.image_id is not null))[1:@coverPictures],
                       '{}') as cover_recipe_ids,
                   coalesce(
                       (array_agg(on_shelf.image_id order by on_shelf.added_at nulls last, on_shelf.id)
                            filter (where on_shelf.image_id is not null))[1:@coverPictures],
                       '{}') as cover_image_ids
            from ({{OnTheShelf}}) as on_shelf
        ) as shelf
        """;

    private static readonly string Shelf = $"""
        select {ShelfColumns}, shelf.recipe_count, shelf.cover_recipe_ids, shelf.cover_image_ids
        from cookbooks c
        {ShelfContents}
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

        // One extra row reveals a next page; the total comes from the same pass so page and count agree.
        var rows = await executor.QueryAsync<CookbookRow>(
            $"""
            with counted as (
                select c.*, count(*) over () as total_count
                from cookbooks c
                where c.household_id = @householdId),
            page as (
                select * from counted
                {(resume is null ? string.Empty : "where (updated_at, id) < (@cursorUpdatedAt, @cursorId)")}
                order by updated_at desc, id desc
                limit {size + 1})
            select {ShelfColumns}, c.total_count,
                   shelf.recipe_count, shelf.cover_recipe_ids, shelf.cover_image_ids
            from page c
            {ShelfContents}
            order by c.updated_at desc, c.id desc;
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

        // The version is in the WHERE: zero rows means somebody wrote first.
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

    public async Task<Result> DeleteAsync(
        Guid cookbookId,
        long expectedVersion,
        Guid deletedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Into the bin: the shelf waits with it until restore or purge.
        var deleted = await executor.ExecuteAsync(
            """
            update cookbooks
            set deleted_at = @now, deleted_by = @deletedBy, version = version + 1
            where id = @cookbookId and version = @expectedVersion;
            """,
            new { cookbookId, expectedVersion, deletedBy, now },
            cancellationToken).ConfigureAwait(false);

        return deleted == 0 ? ConcurrencyErrors.VersionMismatch : Result.Success();
    }

    public async Task<bool> AddRecipeAsync(
        Guid cookbookId,
        Guid recipeId,
        Guid addedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // do nothing, not do update: a recipe already on the shelf keeps when it went on.
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
        // Ids only: answers "already on?" for a whole picker at once.
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
        // Only the shelf's own row (no cover). Both kinds, through the fragment the count and cover use, so a
        // smart cookbook is not invisible on a recipe page.
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

        return [.. rows.Select(ToShelf)];
    }

    // Two arrays in one order: the driver reads uuid arrays, a composite would need its own mapping.
    private static CookbookOnAShelf ToShelf(CookbookRow row) =>
        new(
            ToCookbook(row),
            row.RecipeCount,
            [.. row.CoverRecipeIds.Zip(row.CoverImageIds, (recipeId, imageId) => new CoverPicture(recipeId, imageId))]);

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

    // A stored name that the value object now rejects is a corrupt row: a defect, not a bad request.
    private static CookbookName Unwrap(Result<CookbookName> result) =>
        result.Match(
            name => name,
            error => throw new InvalidOperationException(
                $"Stored cookbook name is not valid ({error.Code}). The row is corrupt."));
}

/// <summary>How a cookbook's kind is spelled in the database: text, so a migration cannot silently reassign rows.</summary>
internal static class CookbookCodes
{
    internal static string Of(CookbookKind kind) => kind == CookbookKind.Smart ? "smart" : "manual";

    internal static CookbookKind ToKind(string stored) =>
        stored == "smart" ? CookbookKind.Smart : CookbookKind.Manual;
}
