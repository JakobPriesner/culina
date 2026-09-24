using Application.Abstractions;
using Domain.Recipes;
using Domain.Shared;

namespace Infrastructure.Persistence.Recipes;

/// <summary>Stores recipes.</summary>
/// <param name="executor">Runs the SQL inside the request's transaction.</param>
/// <param name="tags">Resolves tag slugs to rows.</param>
/// <param name="searcher">Runs the search projection.</param>
/// <param name="documents">Keeps the search document in step with the recipe.</param>
internal sealed class RecipeRepository(
    DbExecutor executor,
    TagWriter tags,
    RecipeSearcher searcher,
    SearchDocumentWriter documents)
    : IRecipeRepository
{
    public Task<RecipePage> SearchAsync(RecipeSearch search, CancellationToken cancellationToken) =>
        searcher.SearchAsync(search, cancellationToken);

    public Task<SearchFacets> FacetsAsync(RecipeSearch search, CancellationToken cancellationToken) =>
        searcher.FacetsAsync(search, cancellationToken);

    public async Task<IReadOnlyList<string>> OwnUnitsAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        // The built-ins are excluded here rather than in C# so the query does
        // the counting: a household with four hundred recipes should not send
        // four hundred rows back to have thirteen of them filtered out.
        var written = await executor.QueryAsync<string>(
            """
            select distinct i.unit
            from recipe_ingredients i
            join ingredient_groups g on g.id = i.group_id
            join recipes r on r.id = g.recipe_id
            where r.household_id = @householdId
              and i.unit is not null
              and lower(i.unit) <> all (@builtIn)
            order by i.unit;
            """,
            new { householdId, builtIn = Unit.BuiltIn.Select(unit => unit.Code).ToArray() },
            cancellationToken).ConfigureAwait(false);

        return [.. written];
    }

    public async Task<IReadOnlyList<string>> OwnIngredientNamesAsync(
        Guid householdId,
        string? query,
        int limit,
        CancellationToken cancellationToken)
    {
        // The most-used spelling of each name wins, which is what stops one
        // stray "Olivenoel" from displacing the "Olivenöl" written forty times.
        // The trigram index on the name column is what makes the LIKE cheap.
        var names = await executor.QueryAsync<string>(
            """
            select i.name
            from recipe_ingredients i
            join ingredient_groups g on g.id = i.group_id
            join recipes r on r.id = g.recipe_id
            where r.household_id = @householdId
              and (@query = '' or i.name ilike '%' || @query || '%')
            group by i.name
            order by (lower(i.name) like lower(@query) || '%') desc, count(*) desc, i.name
            limit @limit;
            """,
            new { householdId, query = query?.Trim() ?? string.Empty, limit },
            cancellationToken).ConfigureAwait(false);

        return [.. names];
    }

    public async Task<Result<Recipe>> FindAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        // One round trip for the whole aggregate. A recipe is never useful
        // without its ingredients, so a query per collection would be four
        // round trips to render one page.
        var reader = await executor.QueryMultipleAsync(
            """
            select id, household_id, title, description, language as recipe_language,
                   yield_amount, yield_kind, yield_label, prep_minutes, cook_minutes, image_id,
                   created_by, created_at, updated_at, version
            from recipes where id = @recipeId;

            select id, recipe_id, name, sort_order
            from ingredient_groups where recipe_id = @recipeId order by sort_order;

            select i.id, i.group_id, i.sort_order, i.quantity, i.unit, i.name, i.note
            from recipe_ingredients i
            join ingredient_groups g on g.id = i.group_id
            where g.recipe_id = @recipeId
            order by i.sort_order;

            select id, recipe_id, sort_order, title, body, duration_seconds
            from steps where recipe_id = @recipeId order by sort_order;

            select r.step_id, r.recipe_ingredient_id
            from step_ingredient_refs r
            join steps s on s.id = r.step_id
            where s.recipe_id = @recipeId;

            select t.slug from recipe_tags rt
            join tags t on t.id = rt.tag_id
            where rt.recipe_id = @recipeId order by t.slug;
            """,
            new { recipeId },
            cancellationToken).ConfigureAwait(false);

        await using (reader.ConfigureAwait(false))
        {
            var row = await reader.ReadSingleOrDefaultAsync<RecipeRow>().ConfigureAwait(false);

            if (row is null)
            {
                return RecipeErrors.NotFound(recipeId);
            }

            var groups = await reader.ReadAsync<IngredientGroupRow>().ConfigureAwait(false);
            var ingredients = await reader.ReadAsync<RecipeIngredientRow>().ConfigureAwait(false);
            var steps = await reader.ReadAsync<StepRow>().ConfigureAwait(false);
            var uses = await reader.ReadAsync<StepIngredientRefRow>().ConfigureAwait(false);
            var slugs = await reader.ReadAsync<string>().ConfigureAwait(false);

            return RecipeAssembler.Assemble(
                [.. groups],
                [.. ingredients],
                [.. steps],
                [.. uses],
                [.. slugs],
                row);
        }
    }

    public async Task<Result> AddAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        await executor.ExecuteAsync(
            """
            insert into recipes (id, household_id, title, description, language, yield_amount,
                                 yield_kind, yield_label, prep_minutes, cook_minutes, image_id,
                                 created_by, created_at, updated_at, version)
            values (@id, @householdId, @title, @description, @language, @yieldAmount,
                    @yieldKind, @yieldLabel, @prepMinutes, @cookMinutes, @imageId,
                    @createdBy, @createdAt, @updatedAt, 1);
            """,
            Parameters(recipe),
            cancellationToken).ConfigureAwait(false);

        await ReplaceContentsAsync(recipe, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<long>> UpdateAsync(
        Recipe recipe,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        var version = await executor.ExecuteScalarAsync<long?>(
            """
            update recipes
            set title = @title, description = @description, language = @language,
                yield_amount = @yieldAmount, yield_kind = @yieldKind,
                yield_label = @yieldLabel,
                prep_minutes = @prepMinutes, cook_minutes = @cookMinutes,
                image_id = @imageId, updated_at = @updatedAt, version = version + 1
            where id = @id and version = @expectedVersion
            returning version;
            """,
            Parameters(recipe, expectedVersion),
            cancellationToken).ConfigureAwait(false);

        if (version is null)
        {
            return ConcurrencyErrors.VersionMismatch;
        }

        await ReplaceContentsAsync(recipe, cancellationToken).ConfigureAwait(false);

        return version.Value;
    }

    public async Task<bool> IsImageStillUsedAsync(
        string contentHash,
        CancellationToken cancellationToken) =>
        await executor.ExecuteScalarAsync<bool>(
            "select exists (select 1 from recipe_images where content_hash = @contentHash);",
            new { contentHash },
            cancellationToken).ConfigureAwait(false);

    public async Task<Result<ImageReplacement>> SetImageAsync(
        Guid recipeId,
        StoredImage image,
        string contentType,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);

        var previous = await PreviousHashAsync(recipeId, cancellationToken).ConfigureAwait(false);
        var imageId = CulinaId.New();

        // The row is replaced rather than accumulated: a recipe has one hero
        // image, and keeping the old row would leave nothing to tell which one
        // is current.
        await executor.ExecuteAsync(
            """
            delete from recipe_images where recipe_id = @recipeId;

            insert into recipe_images
                (id, recipe_id, content_hash, width, height, byte_size, content_type, created_at)
            values (@id, @recipeId, @contentHash, @width, @height, @byteSize, @contentType, @now);

            update recipes set image_id = @id, updated_at = @now, version = version + 1
            where id = @recipeId;
            """,
            new
            {
                id = imageId,
                recipeId,
                contentHash = image.ContentHash,
                width = image.Width,
                height = image.Height,
                byteSize = image.ByteSize,
                contentType,
                now
            },
            cancellationToken).ConfigureAwait(false);

        return new ImageReplacement(previous);
    }

    public async Task<Result<ImageReplacement>> RemoveImageAsync(
        Guid recipeId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var previous = await PreviousHashAsync(recipeId, cancellationToken).ConfigureAwait(false);

        await executor.ExecuteAsync(
            """
            delete from recipe_images where recipe_id = @recipeId;

            update recipes set image_id = null, updated_at = @now, version = version + 1
            where id = @recipeId;
            """,
            new { recipeId, now },
            cancellationToken).ConfigureAwait(false);

        return new ImageReplacement(previous);
    }

    public async Task<Result<string>> ImageHashAsync(
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        var hash = await PreviousHashAsync(recipeId, cancellationToken).ConfigureAwait(false);

        return hash is null ? ImageErrors.NotFound : hash;
    }

    private Task<string?> PreviousHashAsync(Guid recipeId, CancellationToken cancellationToken) =>
        executor.ExecuteScalarAsync<string?>(
            "select content_hash from recipe_images where recipe_id = @recipeId;",
            new { recipeId },
            cancellationToken);

    public async Task<Result> DeleteAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        await executor.ExecuteAsync(
            "delete from recipes where id = @recipeId;",
            new { recipeId },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static object Parameters(Recipe recipe, long? expectedVersion = null) => new
    {
        id = recipe.Id,
        householdId = recipe.HouseholdId,
        title = recipe.Title.Value,
        description = recipe.Description,
        language = RecipeCodes.Of(recipe.Language),
        yieldAmount = recipe.Yield.Amount,
        yieldKind = RecipeCodes.Of(recipe.Yield.Kind),
        yieldLabel = recipe.Yield.Label,
        prepMinutes = recipe.PrepMinutes,
        cookMinutes = recipe.CookMinutes,
        imageId = recipe.ImageId,
        createdBy = recipe.CreatedBy,
        createdAt = recipe.CreatedAt,
        updatedAt = recipe.UpdatedAt,
        expectedVersion
    };

    /// <summary>
    /// Rewrites the ingredient list, the steps and the tag links wholesale.
    /// </summary>
    /// <remarks>
    /// Replacing rather than diffing: a recipe has tens of rows, not thousands,
    /// and a diff has to be right about identity, ordering and removal all at
    /// once. This runs inside the caller's transaction, so the delete and the
    /// inserts are never observed apart.
    /// </remarks>
    private async Task ReplaceContentsAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        // Cascades clear ingredients and the reference index with the groups.
        await executor.ExecuteAsync(
            """
            delete from ingredient_groups where recipe_id = @recipeId;
            delete from steps where recipe_id = @recipeId;
            delete from recipe_tags where recipe_id = @recipeId;
            """,
            new { recipeId = recipe.Id },
            cancellationToken).ConfigureAwait(false);

        foreach (var group in recipe.Groups)
        {
            await executor.ExecuteAsync(
                """
                insert into ingredient_groups (id, recipe_id, name, sort_order)
                values (@id, @recipeId, @name, @sortOrder);
                """,
                new { id = group.Id, recipeId = recipe.Id, name = group.Name, sortOrder = group.SortOrder },
                cancellationToken).ConfigureAwait(false);

            foreach (var ingredient in group.Ingredients)
            {
                await executor.ExecuteAsync(
                    """
                    insert into recipe_ingredients (id, group_id, sort_order, quantity, unit, name, note)
                    values (@id, @groupId, @sortOrder, @quantity, @unit, @name, @note);
                    """,
                    new
                    {
                        id = ingredient.Id,
                        groupId = group.Id,
                        sortOrder = ingredient.SortOrder,
                        quantity = ingredient.Quantity.Amount,
                        unit = RecipeCodes.Of(ingredient.Quantity.Unit),
                        name = ingredient.Name,
                        note = ingredient.Note
                    },
                    cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (var step in recipe.Steps)
        {
            await executor.ExecuteAsync(
                """
                insert into steps (id, recipe_id, sort_order, title, body, duration_seconds)
                values (@id, @recipeId, @sortOrder, @title, @body, @durationSeconds);
                """,
                new
                {
                    id = step.Id,
                    recipeId = recipe.Id,
                    sortOrder = step.SortOrder,
                    title = step.Title,
                    body = StepText.Serialise(step.Segments),
                    durationSeconds = step.DurationSeconds
                },
                cancellationToken).ConfigureAwait(false);

            // The step's own set, which Step.Create has already widened to
            // include everything the sentence mentions — so this cannot
            // disagree with the words, and it is what the read path reads back.
            foreach (var ingredientId in step.Uses)
            {
                await executor.ExecuteAsync(
                    """
                    insert into step_ingredient_refs (step_id, recipe_ingredient_id)
                    values (@stepId, @ingredientId);
                    """,
                    new { stepId = step.Id, ingredientId },
                    cancellationToken).ConfigureAwait(false);
            }
        }

        await tags.LinkAsync(recipe, cancellationToken).ConfigureAwait(false);

        // Last, and inside this same transaction: the document is built by the
        // database from the rows above, so it has to be built after they are
        // there and before anybody else can see them. Both write paths reach
        // this method, which is why the index cannot be forgotten on one of
        // them — and an architecture test says so.
        await documents.WriteAsync(recipe.Id, cancellationToken).ConfigureAwait(false);
    }
}
