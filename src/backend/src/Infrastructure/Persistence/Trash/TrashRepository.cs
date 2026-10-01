using Application.Abstractions;
using Domain.Households;
using Infrastructure.Persistence.Households;
using Infrastructure.Persistence.Recipes;

namespace Infrastructure.Persistence.Trash;

/// <summary>Reads and empties the bin.</summary>
/// <remarks>
/// The one place that reads the <c>*_with_deleted</c> tables rather than the
/// views every other repository uses. Writes that restore go to the tables too,
/// since the views cannot see the rows being restored.
/// </remarks>
/// <param name="executor">Runs the SQL inside the request's transaction.</param>
/// <param name="documents">Makes a restored recipe findable again.</param>
internal sealed class TrashRepository(DbExecutor executor, SearchDocumentWriter documents) : ITrashRepository
{
    public async Task<IReadOnlyList<TrashedItem>> ForHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var rows = await executor.QueryAsync<TrashedRow>(
            """
            select 'recipe' as kind, r.id, r.title as name, r.deleted_at, u.display_name as deleted_by
            from recipes_with_deleted r
            left join users u on u.id = r.deleted_by
            where r.household_id = @householdId and r.deleted_at is not null
            union all
            select 'cookbook', c.id, c.name, c.deleted_at, u.display_name
            from cookbooks_with_deleted c
            left join users u on u.id = c.deleted_by
            where c.household_id = @householdId and c.deleted_at is not null
            order by deleted_at desc, id;
            """,
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => new TrashedItem(
            row.Kind == "cookbook" ? TrashedKind.Cookbook : TrashedKind.Recipe,
            row.Id,
            row.Name,
            row.DeletedAt,
            row.DeletedBy))];
    }

    public async Task<IReadOnlyList<DeletedHousehold>> DeletedHouseholdsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var reader = await executor.QueryMultipleAsync(
            """
            select h.id, h.name, h.created_at, h.version, h.inherits_from, h.deleted_at
            from households_with_deleted h
            join household_members m on m.household_id = h.id
            where m.user_id = @userId and m.role = 'owner' and h.deleted_at is not null
            order by h.deleted_at desc;

            select m.household_id, m.user_id, m.role, m.joined_at
            from household_members m
            join households_with_deleted h on h.id = m.household_id
            where h.deleted_at is not null and m.household_id in (
                select household_id from household_members where user_id = @userId and role = 'owner');
            """,
            new { userId },
            cancellationToken).ConfigureAwait(false);

        await using (reader.ConfigureAwait(false))
        {
            var rows = await reader.ReadAsync<DeletedHouseholdRow>().ConfigureAwait(false);
            var members = (await reader.ReadAsync<HouseholdMemberRow>().ConfigureAwait(false)).ToList();

            return [.. rows.Select(row => new DeletedHousehold(
                row.ToHouseholdRow().ToDomain(members.Where(member => member.HouseholdId == row.Id)),
                row.DeletedAt))];
        }
    }

    public async Task<Household?> DeletedHouseholdAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var reader = await executor.QueryMultipleAsync(
            """
            select id, name, created_at, version, inherits_from, deleted_at
            from households_with_deleted
            where id = @householdId and deleted_at is not null;

            select household_id, user_id, role, joined_at
            from household_members where household_id = @householdId;
            """,
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        await using (reader.ConfigureAwait(false))
        {
            var row = await reader.ReadSingleOrDefaultAsync<DeletedHouseholdRow>().ConfigureAwait(false);
            var members = await reader.ReadAsync<HouseholdMemberRow>().ConfigureAwait(false);

            return row?.ToHouseholdRow().ToDomain(members);
        }
    }

    // Joined to the households view: a recipe in the bin of a household that
    // is itself in the bin comes back with the household, not on its own.
    public Task<Guid?> HouseholdOfDeletedRecipeAsync(Guid recipeId, CancellationToken cancellationToken) =>
        executor.ExecuteScalarAsync<Guid?>(
            """
            select r.household_id from recipes_with_deleted r
            join households h on h.id = r.household_id
            where r.id = @recipeId and r.deleted_at is not null;
            """,
            new { recipeId },
            cancellationToken);

    public Task<Guid?> HouseholdOfDeletedCookbookAsync(Guid cookbookId, CancellationToken cancellationToken) =>
        executor.ExecuteScalarAsync<Guid?>(
            """
            select c.household_id from cookbooks_with_deleted c
            join households h on h.id = c.household_id
            where c.id = @cookbookId and c.deleted_at is not null;
            """,
            new { cookbookId },
            cancellationToken);

    public async Task<bool> RestoreRecipeAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        var restored = await RestoreAsync("recipes_with_deleted", recipeId, cancellationToken).ConfigureAwait(false);

        if (restored)
        {
            // Deleting dropped the document; nothing finds the recipe until it
            // is written again.
            await documents.WriteAsync(recipeId, cancellationToken).ConfigureAwait(false);
        }

        return restored;
    }

    public Task<bool> RestoreCookbookAsync(Guid cookbookId, CancellationToken cancellationToken) =>
        RestoreAsync("cookbooks_with_deleted", cookbookId, cancellationToken);

    public Task<bool> RestoreHouseholdAsync(Guid householdId, CancellationToken cancellationToken) =>
        RestoreAsync("households_with_deleted", householdId, cancellationToken);

    public async Task<PurgedTrash> PurgeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        // The pictures first, while the rows that point at them still exist:
        // the recipes about to go, whether binned themselves or with their
        // household, and the photos of the times they were cooked.
        var candidates = await executor.QueryAsync<string>(
            """
            with doomed as (
                select r.id from recipes_with_deleted r
                join households_with_deleted h on h.id = r.household_id
                where r.deleted_at < @cutoff or h.deleted_at < @cutoff)
            select content_hash from recipe_images where recipe_id in (select id from doomed)
            union
            select image_hash from cook_log_entries
            where recipe_id in (select id from doomed) and image_hash is not null;
            """,
            new { cutoff },
            cancellationToken).ConfigureAwait(false);

        // Households first, so their recipes and cookbooks go by cascade, then
        // whatever was binned on its own inside a household that stayed.
        var removed = 0;

        foreach (var table in (string[])["households_with_deleted", "recipes_with_deleted", "cookbooks_with_deleted"])
        {
            removed += await executor.ExecuteAsync(
                $"delete from {table} where deleted_at < @cutoff;",
                new { cutoff },
                cancellationToken).ConfigureAwait(false);
        }

        // Storage is content-addressed, so another recipe — or a recipe still
        // in the bin — may share a file. Only the ones nobody points at go.
        var released = await executor.QueryAsync<string>(
            """
            select hash from unnest(@candidates::text[]) as hash
            where not exists (select 1 from recipe_images where content_hash = hash)
              and not exists (select 1 from cook_log_entries where image_hash = hash);
            """,
            new { candidates = candidates.ToArray() },
            cancellationToken).ConfigureAwait(false);

        return new PurgedTrash(removed, released);
    }

    private async Task<bool> RestoreAsync(string table, Guid id, CancellationToken cancellationToken) =>
        await executor.ExecuteAsync(
            $"""
             update {table}
             set deleted_at = null, deleted_by = null, version = version + 1
             where id = @id and deleted_at is not null;
             """,
            new { id },
            cancellationToken).ConfigureAwait(false) > 0;

    private sealed record TrashedRow
    {
        public string Kind { get; init; } = string.Empty;

        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public DateTimeOffset DeletedAt { get; init; }

        public string? DeletedBy { get; init; }
    }

    private sealed record DeletedHouseholdRow
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public long Version { get; init; }

        public Guid? InheritsFrom { get; init; }

        public DateTimeOffset DeletedAt { get; init; }

        public HouseholdRow ToHouseholdRow() => new()
        {
            Id = Id,
            Name = Name,
            CreatedAt = CreatedAt,
            Version = Version,
            InheritsFrom = InheritsFrom
        };
    }
}
