using Application.Abstractions;

namespace Infrastructure.Persistence.Tags;

/// <summary>Reads the vocabulary a household's recipes have built up.</summary>
/// <param name="executor">Runs the SQL.</param>
internal sealed class TagRepository(DbExecutor executor) : ITagRepository
{
    public async Task<IReadOnlyList<TagUsage>> InUseAsync(
        IReadOnlyList<Guid> library,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);

        // Most used first, then alphabetical: stable tie-breaks stop the list reshuffling between identical reads.
        var rows = await executor.QueryAsync<TagUsage>(
            """
            select t.slug, min(t.name) as name, count(rt.recipe_id)::int as recipe_count
            from tags t
            join recipe_tags rt on rt.tag_id = t.id
            -- The view, so a binned recipe neither counts towards a tag nor keeps one on offer that filters to nothing.
            join recipes r on r.id = rt.recipe_id
            where t.household_id = any(@library)
            -- By slug: a household and one it inherits from may both carry "vegan", and a chip is for the word.
            group by t.slug
            order by count(rt.recipe_id) desc, t.slug;
            """,
            new { library = library.ToArray() },
            cancellationToken).ConfigureAwait(false);

        return rows;
    }
}
