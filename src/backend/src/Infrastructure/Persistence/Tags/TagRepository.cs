using Application.Abstractions;

namespace Infrastructure.Persistence.Tags;

/// <summary>Reads the vocabulary a household's recipes have built up.</summary>
/// <param name="executor">Runs the SQL.</param>
internal sealed class TagRepository(DbExecutor executor) : ITagRepository
{
    public async Task<IReadOnlyList<TagUsage>> InUseAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        // Most used first, then alphabetical. The words this kitchen reaches
        // for are the ones worth offering, and ties have to break somewhere
        // stable or the list reshuffles itself between two identical reads.
        var rows = await executor.QueryAsync<TagUsage>(
            """
            select t.slug, t.name, count(rt.recipe_id)::int as recipe_count
            from tags t
            join recipe_tags rt on rt.tag_id = t.id
            where t.household_id = @householdId
            group by t.slug, t.name
            order by count(rt.recipe_id) desc, t.slug;
            """,
            new { householdId },
            cancellationToken).ConfigureAwait(false);

        return rows;
    }
}
