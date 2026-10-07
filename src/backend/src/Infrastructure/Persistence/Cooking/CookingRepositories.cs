using Application.Abstractions;
using Domain.Cooking;
using Domain.Shared;

namespace Infrastructure.Persistence.Cooking;

/// <summary>The <c>personal_notes</c> row.</summary>
internal sealed record PersonalNoteRow
{
    public Guid Id { get; init; }

    public Guid RecipeId { get; init; }

    public Guid UserId { get; init; }

    public Guid? StepId { get; init; }

    public string Body { get; init; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>The <c>cook_log_entries</c> row.</summary>
internal sealed record CookLogRow
{
    public Guid Id { get; init; }

    public Guid RecipeId { get; init; }

    public Guid UserId { get; init; }

    public Guid HouseholdId { get; init; }

    public DateTimeOffset MadeAt { get; init; }

    public decimal? Servings { get; init; }

    public string? Note { get; init; }

    public string? ImageHash { get; init; }

    public int? ImageWidth { get; init; }

    public int? ImageHeight { get; init; }
}

/// <summary>Stores one person's notes.</summary>
internal sealed class PersonalNoteRepository(DbExecutor executor) : IPersonalNoteRepository
{
    public async Task<IReadOnlyList<PersonalNote>> ForRecipeAsync(
        Guid recipeId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Scoped to the caller in SQL, so "only your own notes" cannot be forgotten at a second call site.
        var rows = await executor.QueryAsync<PersonalNoteRow>(
            """
            select id, recipe_id, user_id, step_id, body, updated_at
            from personal_notes
            where recipe_id = @recipeId and user_id = @userId
            order by step_id nulls first;
            """,
            new { recipeId, userId },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => PersonalNote.Restore(
            row.Id, row.RecipeId, row.UserId, row.StepId, row.Body, row.UpdatedAt))];
    }

    public async Task<ILookup<Guid, PersonalNote>> ForRecipesAsync(
        IReadOnlyCollection<Guid> recipeIds,
        Guid userId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipeIds);

        var rows = await executor.QueryAsync<PersonalNoteRow>(
            """
            select id, recipe_id, user_id, step_id, body, updated_at
            from personal_notes
            where recipe_id = any(@recipeIds) and user_id = @userId
            order by step_id nulls first;
            """,
            new { recipeIds = recipeIds.ToArray(), userId },
            cancellationToken).ConfigureAwait(false);

        return rows.ToLookup(
            row => row.RecipeId,
            row => PersonalNote.Restore(row.Id, row.RecipeId, row.UserId, row.StepId, row.Body, row.UpdatedAt));
    }

    public async Task<Result> ReplaceAsync(
        Guid recipeId,
        Guid userId,
        IReadOnlyList<PersonalNote> notes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notes);

        await executor.ExecuteAsync(
            "delete from personal_notes where recipe_id = @recipeId and user_id = @userId;",
            new { recipeId, userId },
            cancellationToken).ConfigureAwait(false);

        if (notes.Count > 0)
        {
            await executor.ExecuteAsync(
                """
                insert into personal_notes (id, recipe_id, user_id, step_id, body, updated_at)
                select id, @recipeId, @userId, step_id, body, updated_at
                from unnest(@ids::uuid[], @stepIds::uuid[], @bodies::text[], @updatedAts::timestamptz[])
                    as n(id, step_id, body, updated_at);
                """,
                new
                {
                    recipeId,
                    userId,
                    ids = notes.Select(note => note.Id).ToArray(),
                    stepIds = notes.Select(note => note.StepId).ToArray(),
                    bodies = notes.Select(note => note.Body).ToArray(),
                    updatedAts = notes.Select(note => note.UpdatedAt).ToArray()
                },
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }
}

/// <summary>Stores what someone has cooked.</summary>
internal sealed class CookLogRepository(DbExecutor executor) : ICookLogRepository
{
    public async Task<IReadOnlyList<CookLogEntry>> ForRecipeAsync(
        Guid recipeId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync<CookLogRow>(
            """
            select id, recipe_id, user_id, household_id, made_at, servings, note,
                   image_hash, image_width, image_height
            from cook_log_entries
            where recipe_id = @recipeId and user_id = @userId
            order by made_at desc;
            """,
            new { recipeId, userId },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(ToEntry)];
    }

    public async Task<ILookup<Guid, CookLogEntry>> ForRecipesAsync(
        IReadOnlyCollection<Guid> recipeIds,
        Guid userId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipeIds);

        var rows = await executor.QueryAsync<CookLogRow>(
            """
            select id, recipe_id, user_id, household_id, made_at, servings, note,
                   image_hash, image_width, image_height
            from cook_log_entries
            where recipe_id = any(@recipeIds) and user_id = @userId
            order by made_at desc;
            """,
            new { recipeIds = recipeIds.ToArray(), userId },
            cancellationToken).ConfigureAwait(false);

        return rows.ToLookup(row => row.RecipeId, ToEntry);
    }

    // The three photo columns move together (check constraint) but a half-written row reads as no picture rather than throwing.
    private static CookLogEntry ToEntry(CookLogRow row) => CookLogEntry.Restore(
        row.Id,
        row.RecipeId,
        row.UserId,
        row.HouseholdId,
        row.MadeAt,
        row.Servings,
        row.Note,
        row is { ImageHash: { } hash, ImageWidth: { } width, ImageHeight: { } height }
            ? new CookPhoto(hash, width, height)
            : null);

    public async Task<Result<CookLogEntry>> FindAsync(
        Guid entryId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Scoped to the caller in SQL: another's attempt is not found rather than forbidden.
        var row = await executor.QuerySingleOrDefaultAsync<CookLogRow>(
            """
            select id, recipe_id, user_id, household_id, made_at, servings, note,
                   image_hash, image_width, image_height
            from cook_log_entries
            where id = @entryId and user_id = @userId;
            """,
            new { entryId, userId },
            cancellationToken).ConfigureAwait(false);

        return row is null ? CookingErrors.EntryNotFound : ToEntry(row);
    }

    public async Task<Result> SetPhotoAsync(
        Guid entryId,
        Guid userId,
        CookPhoto? photo,
        CancellationToken cancellationToken)
    {
        var affected = await executor.ExecuteAsync(
            """
            update cook_log_entries
            set image_hash = @hash, image_width = @width, image_height = @height
            where id = @entryId and user_id = @userId;
            """,
            new
            {
                entryId,
                userId,
                hash = photo?.ContentHash,
                width = photo?.Width,
                height = photo?.Height
            },
            cancellationToken).ConfigureAwait(false);

        return affected == 0 ? CookingErrors.EntryNotFound : Result.Success();
    }

    public async Task<Result> AddAsync(CookLogEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await executor.ExecuteAsync(
            """
            insert into cook_log_entries (id, recipe_id, user_id, household_id, made_at, servings, note)
            values (@id, @recipeId, @userId, @householdId, @madeAt, @servings, @note);
            """,
            new
            {
                id = entry.Id,
                recipeId = entry.RecipeId,
                userId = entry.UserId,
                householdId = entry.HouseholdId,
                madeAt = entry.MadeAt,
                servings = entry.Servings,
                note = entry.Note
            },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> RemoveAsync(
        Guid entryId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var affected = await executor.ExecuteAsync(
            "delete from cook_log_entries where id = @entryId and user_id = @userId;",
            new { entryId, userId },
            cancellationToken).ConfigureAwait(false);

        return affected == 0 ? CookingErrors.EntryNotFound : Result.Success();
    }
}
