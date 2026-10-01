using Application.Abstractions;
using Domain.Households;
using Domain.Shared;

namespace Infrastructure.Persistence.Households;

/// <summary>Stores households and their membership.</summary>
/// <param name="executor">Runs the SQL inside the request's transaction.</param>
internal sealed class HouseholdRepository(DbExecutor executor) : IHouseholdRepository
{
    public async Task<Result<Household>> FindAsync(Guid householdId, CancellationToken cancellationToken)
    {
        // One round trip for the aggregate rather than a query per collection:
        // a household is never useful without its members.
        var reader = await executor.QueryMultipleAsync(
            """
            select id, name, created_at, version, inherits_from from households where id = @householdId;
            select household_id, user_id, role, joined_at
            from household_members where household_id = @householdId;
            """,
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        await using (reader.ConfigureAwait(false))
        {
            var row = await reader.ReadSingleOrDefaultAsync<HouseholdRow>().ConfigureAwait(false);

            if (row is null)
            {
                return HouseholdErrors.NotFound(householdId);
            }

            var members = await reader.ReadAsync<HouseholdMemberRow>().ConfigureAwait(false);

            return row.ToDomain(members);
        }
    }

    public async Task<IReadOnlyList<Household>> ForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var reader = await executor.QueryMultipleAsync(
            """
            select h.id, h.name, h.created_at, h.version, h.inherits_from
            from households h
            join household_members m on m.household_id = h.id
            where m.user_id = @userId
            order by h.name;

            select m.household_id, m.user_id, m.role, m.joined_at
            from household_members m
            where m.household_id in (
                select household_id from household_members where user_id = @userId);
            """,
            new { userId },
            cancellationToken).ConfigureAwait(false);

        await using (reader.ConfigureAwait(false))
        {
            var rows = await reader.ReadAsync<HouseholdRow>().ConfigureAwait(false);
            var members = (await reader.ReadAsync<HouseholdMemberRow>().ConfigureAwait(false)).ToList();

            return [.. rows.Select(row =>
                row.ToDomain(members.Where(member => member.HouseholdId == row.Id)))];
        }
    }

    public async Task<Result> AddAsync(Household household, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(household);

        await executor.ExecuteAsync(
            """
            insert into households (id, name, created_at, version, inherits_from)
            values (@id, @name, @createdAt, 1, @inheritsFrom);
            """,
            new
            {
                id = household.Id,
                name = household.Name.Value,
                createdAt = household.CreatedAt,
                inheritsFrom = household.InheritsFrom
            },
            cancellationToken).ConfigureAwait(false);

        await ReplaceMembersAsync(household, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<long>> UpdateAsync(
        Household household,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(household);

        var version = await executor.ExecuteScalarAsync<long?>(
            """
            update households
            set name = @name, inherits_from = @inheritsFrom, version = version + 1
            where id = @id and version = @expectedVersion
            returning version;
            """,
            new
            {
                id = household.Id,
                name = household.Name.Value,
                inheritsFrom = household.InheritsFrom,
                expectedVersion
            },
            cancellationToken).ConfigureAwait(false);

        if (version is null)
        {
            return ConcurrencyErrors.VersionMismatch;
        }

        await ReplaceMembersAsync(household, cancellationToken).ConfigureAwait(false);

        return version.Value;
    }

    public async Task<bool> IsMemberAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await executor.ExecuteScalarAsync<bool>(
            """
            select exists (
                select 1 from household_members m
                -- The view, so a deleted household has no members as far as
                -- anything that asks is concerned. The rows stay, because they
                -- are who may restore it.
                join households h on h.id = m.household_id
                where m.household_id = @householdId and m.user_id = @userId);
            """,
            new { householdId, userId },
            cancellationToken).ConfigureAwait(false);

    public async Task<bool> CanSeeRecipesAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken) =>
        // Downwards from the owner, through everything that inherits from it:
        // the caller is in one of those or sees nothing. union rather than
        // union all, so a repeated household ends the walk instead of looping.
        await executor.ExecuteScalarAsync<bool>(
            """
            with recursive heirs (id) as (
                select id from households where id = @householdId
                union
                select h.id from households h join heirs on h.inherits_from = heirs.id
            )
            select exists (
                select 1 from household_members m
                join heirs on heirs.id = m.household_id
                where m.user_id = @userId);
            """,
            new { householdId, userId },
            cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<InheritedHousehold>> AncestorsAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        // Upwards, one parent at a time. The cycle clause is belt and braces:
        // the application refuses a loop, and this stops at a repeat anyway
        // rather than walking one forever.
        var rows = await executor.QueryAsync<InheritedHouseholdRow>(
            """
            with recursive chain (id, name, inherits_from, depth) as (
                select p.id, p.name, p.inherits_from, 1
                from households h
                join households p on p.id = h.inherits_from
                where h.id = @householdId
                union all
                select p.id, p.name, p.inherits_from, c.depth + 1
                from chain c
                join households p on p.id = c.inherits_from
            ) cycle id set looped using path
            select id, name from chain
            where not looped and id <> @householdId
            order by depth;
            """,
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => new InheritedHousehold(row.Id, row.Name))];
    }

    public async Task<IReadOnlyList<Heir>> HeirsAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        // Downwards, the way CanSeeRecipesAsync walks: everybody listed here
        // is somebody who can read this household's recipes.
        var rows = await executor.QueryAsync<HeirRow>(
            """
            with recursive heirs (id, name, inherits_from, depth) as (
                select h.id, h.name, h.inherits_from, 1
                from households h
                where h.inherits_from = @householdId
                union all
                select h.id, h.name, h.inherits_from, d.depth + 1
                from households h
                join heirs d on h.inherits_from = d.id
            ) cycle id set looped using path
            select id, name, inherits_from from heirs
            where not looped and id <> @householdId
            order by depth, name;
            """,
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => new Heir(row.Id, row.Name, row.InheritsFrom))];
    }

    public async Task<IReadOnlyList<HouseholdMemberView>> MembersAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        // Joined in SQL rather than loading a user per member: the members
        // screen is the only place a name is needed, and the domain has no
        // business carrying one.
        var rows = await executor.QueryAsync<HouseholdMemberViewRow>(
            """
            select m.user_id, u.display_name, m.role, m.joined_at
            from household_members m
            join users u on u.id = m.user_id
            where m.household_id = @householdId
            order by m.joined_at;
            """,
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => row.ToView())];
    }

    public async Task<Result> DeleteAsync(
        Guid householdId,
        long expectedVersion,
        Guid deletedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Into the bin, not gone: the households view stops showing it, and
        // its recipes and cookbooks with it, until it is restored or the
        // purge removes it — and only then do recipes, tags and the shopping
        // list cascade. Through the view, so a household already in the bin
        // is not deleted twice.
        var deleted = await executor.ExecuteAsync(
            """
            update households
            set deleted_at = @now, deleted_by = @deletedBy, version = version + 1
            where id = @householdId and version = @expectedVersion;
            """,
            new { householdId, expectedVersion, deletedBy, now },
            cancellationToken).ConfigureAwait(false);

        return deleted == 0 ? ConcurrencyErrors.VersionMismatch : Result.Success();
    }

    /// <summary>
    /// Rewrites the membership list wholesale.
    /// </summary>
    /// <remarks>
    /// A household has a handful of members, so replacing them is simpler than
    /// diffing and cannot drift from the aggregate the domain just validated.
    /// It runs inside the caller's transaction, so the delete and the inserts
    /// are never observed apart.
    /// </remarks>
    private async Task ReplaceMembersAsync(Household household, CancellationToken cancellationToken)
    {
        await executor.ExecuteAsync(
            "delete from household_members where household_id = @householdId;",
            new { householdId = household.Id },
            cancellationToken).ConfigureAwait(false);

        foreach (var member in household.Members)
        {
            await executor.ExecuteAsync(
                """
                insert into household_members (household_id, user_id, role, joined_at)
                values (@householdId, @userId, @role, @joinedAt);
                """,
                new
                {
                    householdId = household.Id,
                    userId = member.UserId,
                    role = member.Role.ToStorage(),
                    joinedAt = member.JoinedAt
                },
                cancellationToken).ConfigureAwait(false);
        }
    }
}
