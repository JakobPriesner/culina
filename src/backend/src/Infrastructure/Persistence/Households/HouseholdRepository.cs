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
            select id, name, created_at, version from households where id = @householdId;
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
            select h.id, h.name, h.created_at, h.version
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
            insert into households (id, name, created_at, version)
            values (@id, @name, @createdAt, 1);
            """,
            new { id = household.Id, name = household.Name.Value, createdAt = household.CreatedAt },
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
            set name = @name, version = version + 1
            where id = @id and version = @expectedVersion
            returning version;
            """,
            new { id = household.Id, name = household.Name.Value, expectedVersion },
            cancellationToken).ConfigureAwait(false);

        if (version is null)
        {
            return ConcurrencyErrors.VersionMismatch;
        }

        await ReplaceMembersAsync(household, cancellationToken).ConfigureAwait(false);

        return version.Value;
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

    public async Task<Result> DeleteAsync(Guid householdId, CancellationToken cancellationToken)
    {
        // Recipes, tags and the shopping list cascade from here; there is no
        // soft delete, because an undo affordance in the UI is a better answer
        // than a deleted_at column every query has to remember.
        await executor.ExecuteAsync(
            "delete from households where id = @householdId;",
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
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
