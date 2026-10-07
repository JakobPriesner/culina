using Application.Abstractions;
using Domain.Recipes;
using Domain.Shared;
using Npgsql;

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
        IReadOnlyList<Guid> library,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);

        // The built-ins are excluded here rather than in C# so the query does
        // the counting: a household with four hundred recipes should not send
        // four hundred rows back to have thirteen of them filtered out.
        var written = await executor.QueryAsync<string>(
            """
            select distinct i.unit
            from recipe_ingredients i
            join ingredient_groups g on g.id = i.group_id
            join recipes r on r.id = g.recipe_id
            where r.household_id = any(@library)
              and i.unit is not null
              and lower(i.unit) <> all (@builtIn)
            order by i.unit;
            """,
            new { library = library.ToArray(), builtIn = Unit.BuiltIn.Select(unit => unit.Code).ToArray() },
            cancellationToken).ConfigureAwait(false);

        return [.. written];
    }

    public async Task<IReadOnlyList<string>> OwnIngredientNamesAsync(
        IReadOnlyList<Guid> library,
        string? query,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);

        // The most-used spelling of each name wins, which is what stops one
        // stray "Olivenoel" from displacing the "Olivenöl" written forty times.
        // The trigram index on the name column is what makes the LIKE cheap.
        var names = await executor.QueryAsync<string>(
            """
            select i.name
            from recipe_ingredients i
            join ingredient_groups g on g.id = i.group_id
            join recipes r on r.id = g.recipe_id
            where r.household_id = any(@library)
              and (@query = '' or i.name ilike '%' || @query || '%')
            group by i.name
            order by (lower(i.name) like lower(@query) || '%') desc, count(*) desc, i.name
            limit @limit;
            """,
            new { library = library.ToArray(), query = query?.Trim() ?? string.Empty, limit },
            cancellationToken).ConfigureAwait(false);

        return [.. names];
    }

    public async Task<Result<Guid>> HouseholdOfAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        var household = await executor.ExecuteScalarAsync<Guid?>(
            "select household_id from recipes where id = @recipeId;",
            new { recipeId },
            cancellationToken).ConfigureAwait(false);

        return household is { } found ? found : RecipeErrors.NotFound(recipeId);
    }

    public async Task<Result<Recipe>> FindAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        // One round trip for the whole aggregate. A recipe is never useful
        // without its ingredients, so a query per collection would be four
        // round trips to render one page.
        return await executor.QueryMultipleAsync<Result<Recipe>>(
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
            async reader =>
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
            },
            cancellationToken).ConfigureAwait(false);
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

        return await ReplaceContentsAsync(recipe, cancellationToken).ConfigureAwait(false);
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

        var replaced = await ReplaceContentsAsync(recipe, cancellationToken).ConfigureAwait(false);

        return replaced.Map(() => version.Value);
    }

    public async Task<bool> IsImageStillUsedAsync(
        string contentHash,
        CancellationToken cancellationToken) =>
        await executor.ExecuteScalarAsync<bool>(
            $"select {ImageReferences.StillUsed("@contentHash")};",
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

    public Task CopyImageAsync(
        Guid fromRecipeId,
        Guid toRecipeId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        // Without touching the version: this runs in the transaction that
        // creates the recipe, so no version of it has been seen yet that this
        // could be newer than.
        executor.ExecuteAsync(
            """
            insert into recipe_images
                (id, recipe_id, content_hash, width, height, byte_size, content_type, created_at)
            select @imageId, @toRecipeId, i.content_hash, i.width, i.height, i.byte_size, i.content_type, @now
            from recipes r
            join recipe_images i on i.id = r.image_id
            where r.id = @fromRecipeId;

            update recipes set image_id = @imageId
            where id = @toRecipeId and exists (select 1 from recipe_images where id = @imageId);
            """,
            new { imageId = CulinaId.New(), fromRecipeId, toRecipeId, now },
            cancellationToken);

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

    public async Task<Result> DeleteAsync(
        Guid recipeId,
        long expectedVersion,
        Guid deletedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Into the bin: the recipes view stops showing it. Its search document
        // goes now rather than at the purge, because search, completions, the
        // spelling hints, related recipes and the import's look-alikes all read
        // documents — dropping the one row keeps a binned recipe out of every
        // one of them. Restoring writes it again.
        var deleted = await executor.ExecuteAsync(
            """
            update recipes
            set deleted_at = @now, deleted_by = @deletedBy, version = version + 1
            where id = @recipeId and version = @expectedVersion;
            """,
            new { recipeId, expectedVersion, deletedBy, now },
            cancellationToken).ConfigureAwait(false);

        if (deleted == 0)
        {
            return ConcurrencyErrors.VersionMismatch;
        }

        await executor.ExecuteAsync(
            "delete from recipe_search_documents where recipe_id = @recipeId;",
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
    /// Rewrites the ingredient list and the tag links wholesale, and the steps
    /// in place.
    /// </summary>
    /// <remarks>
    /// Replacing rather than diffing: a recipe has tens of rows, not thousands,
    /// and a diff has to be right about identity, ordering and removal all at
    /// once. Steps are the exception, because people's personal notes hang off
    /// them and a deleted step takes its notes with it — so a step that is kept
    /// is updated, and only a step that is gone is deleted. This runs inside the
    /// caller's transaction, so the deletes and the writes are never observed
    /// apart.
    /// </remarks>
    private async Task<Result> ReplaceContentsAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        try
        {
            await WriteContentsAsync(recipe, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (PostgresException failure) when (TakenId(failure.ConstraintName) is { } error)
        {
            // The ids are the client's, and the recipe has already checked
            // them against each other — but not against every other recipe's
            // rows, which only the primary key can see. An id taken there is
            // the same mistake as one used twice here, and the transaction
            // this runs in rolls back with the failure.
            return error;
        }
    }

    private static Error? TakenId(string? constraint) => constraint switch
    {
        "ingredient_groups_pkey" => RecipeErrors.DuplicateGroup,
        "recipe_ingredients_pkey" => RecipeErrors.DuplicateIngredient,
        "steps_pkey" => RecipeErrors.DuplicateStep,
        _ => null
    };

    private async Task WriteContentsAsync(Recipe recipe, CancellationToken cancellationToken)
    {
        // Cascades clear ingredients and the reference index with the groups.
        await executor.ExecuteAsync(
            """
            delete from ingredient_groups where recipe_id = @recipeId;
            delete from steps where recipe_id = @recipeId and id <> all(@stepIds);
            delete from recipe_tags where recipe_id = @recipeId;
            """,
            new { recipeId = recipe.Id, stepIds = recipe.Steps.Select(step => step.Id).ToArray() },
            cancellationToken).ConfigureAwait(false);

        // Groups and ingredients are one statement each, and the references one
        // more, however big the recipe: a round trip per row made saving a
        // recipe, and importing a thousand of them, a conversation with the
        // database.
        if (recipe.Groups.Count > 0)
        {
            await executor.ExecuteAsync(
                """
                insert into ingredient_groups (id, recipe_id, name, sort_order)
                select id, @recipeId, name, sort_order
                from unnest(@ids::uuid[], @names::text[], @sortOrders::integer[]) as g(id, name, sort_order);
                """,
                new
                {
                    recipeId = recipe.Id,
                    ids = recipe.Groups.Select(group => group.Id).ToArray(),
                    names = recipe.Groups.Select(group => group.Name).ToArray(),
                    sortOrders = recipe.Groups.Select(group => group.SortOrder).ToArray()
                },
                cancellationToken).ConfigureAwait(false);
        }

        var ingredients = recipe.Groups
            .SelectMany(group => group.Ingredients.Select(ingredient => (group, ingredient)))
            .ToArray();

        if (ingredients.Length > 0)
        {
            await executor.ExecuteAsync(
                """
                insert into recipe_ingredients (id, group_id, sort_order, quantity, unit, name, note)
                select id, group_id, sort_order, quantity, unit, name, note
                from unnest(
                    @ids::uuid[], @groupIds::uuid[], @sortOrders::integer[], @quantities::numeric[],
                    @units::text[], @names::text[], @notes::text[])
                    as i(id, group_id, sort_order, quantity, unit, name, note);
                """,
                new
                {
                    ids = ingredients.Select(one => one.ingredient.Id).ToArray(),
                    groupIds = ingredients.Select(one => one.group.Id).ToArray(),
                    sortOrders = ingredients.Select(one => one.ingredient.SortOrder).ToArray(),
                    quantities = ingredients.Select(one => one.ingredient.Quantity.Amount).ToArray(),
                    units = ingredients.Select(one => RecipeCodes.Of(one.ingredient.Quantity.Unit)).ToArray(),
                    names = ingredients.Select(one => one.ingredient.Name).ToArray(),
                    notes = ingredients.Select(one => one.ingredient.Note).ToArray()
                },
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var step in recipe.Steps)
        {
            await WriteStepAsync(recipe.Id, step, cancellationToken).ConfigureAwait(false);
        }

        // Each step's own set, which Step.Create has already widened to
        // include everything the sentence mentions — so this cannot
        // disagree with the words, and it is what the read path reads back.
        var references = recipe.Steps
            .SelectMany(step => step.Uses.Select(ingredientId => (stepId: step.Id, ingredientId)))
            .ToArray();

        if (references.Length > 0)
        {
            await executor.ExecuteAsync(
                """
                insert into step_ingredient_refs (step_id, recipe_ingredient_id)
                select step_id, recipe_ingredient_id
                from unnest(@stepIds::uuid[], @ingredientIds::uuid[]) as r(step_id, recipe_ingredient_id);
                """,
                new
                {
                    stepIds = references.Select(one => one.stepId).ToArray(),
                    ingredientIds = references.Select(one => one.ingredientId).ToArray()
                },
                cancellationToken).ConfigureAwait(false);
        }

        await tags.LinkAsync(recipe, cancellationToken).ConfigureAwait(false);

        // Last, and inside this same transaction: the document is built by the
        // database from the rows above, so it has to be built after they are
        // there and before anybody else can see them. Both write paths reach
        // this method, which is why the index cannot be forgotten on one of
        // them — and an architecture test says so.
        await documents.WriteAsync(recipe.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Updates a step the recipe already has, or adds a new one.</summary>
    /// <remarks>
    /// The update is limited to this recipe's own steps, so an id that belongs
    /// to another recipe falls through to the insert and fails on the primary
    /// key instead of moving that step over.
    /// </remarks>
    private async Task WriteStepAsync(Guid recipeId, Step step, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            id = step.Id,
            recipeId,
            sortOrder = step.SortOrder,
            title = step.Title,
            body = StepText.Serialise(step.Segments),
            durationSeconds = step.DurationSeconds
        };

        var updated = await executor.ExecuteAsync(
            """
            update steps
            set sort_order = @sortOrder, title = @title, body = @body,
                duration_seconds = @durationSeconds
            where id = @id and recipe_id = @recipeId;
            """,
            parameters,
            cancellationToken).ConfigureAwait(false);

        if (updated == 0)
        {
            await executor.ExecuteAsync(
                """
                insert into steps (id, recipe_id, sort_order, title, body, duration_seconds)
                values (@id, @recipeId, @sortOrder, @title, @body, @durationSeconds);
                """,
                parameters,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
