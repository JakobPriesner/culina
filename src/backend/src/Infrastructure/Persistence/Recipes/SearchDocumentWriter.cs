using Domain.Search;

namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// Keeps a recipe's search document in step with the recipe.
/// </summary>
/// <remarks>
/// <para>
/// Called from inside the recipe's own write, so the document and the recipe
/// are never observed apart: a recipe saved on the tablet is findable from the
/// phone immediately, and a write that could not be indexed is a write that
/// failed rather than a recipe that quietly cannot be found. That is the same
/// trade <c>step_ingredient_refs</c> already makes, and the reason is the same
/// — a derived index that lags its source is an index that is occasionally
/// wrong with nothing to say so.
/// </para>
/// <para>
/// The text of the document is built by the database from the rows that were
/// just written, through the <c>recipe_search_input</c> view. Assembling it in
/// C# instead would put the definition of "what is searchable about a recipe"
/// in two places — here and in the migration that has to backfill two thousand
/// of them — and the two would drift the first time somebody added a field to
/// one of them.
/// </para>
/// <para>
/// The concepts are the exception, and for the mirror-image reason: the
/// lexicon they come from is C#, and a copy of it in SQL would be the second
/// place. So the database writes the text, then this reads back the three
/// fields the lexicon looks at and writes what it found.
/// </para>
/// </remarks>
/// <param name="executor">Runs the SQL inside the request's transaction.</param>
internal sealed class SearchDocumentWriter(DbExecutor executor)
{
    /// <summary>
    /// Rebuilds one recipe's document.
    /// </summary>
    /// <param name="recipeId">The recipe that has just been written.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal async Task WriteAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        await executor.ExecuteAsync(Upsert, new { recipeId }, cancellationToken).ConfigureAwait(false);
        await WriteConceptsAsync([recipeId], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rebuilds the concepts of documents a different lexicon built.
    /// </summary>
    /// <returns>How many were rebuilt: fewer than asked for means none are left.</returns>
    /// <param name="batchSize">How many to rebuild in this call.</param>
    /// <param name="cancellationToken">Cancels between rows.</param>
    internal async Task<int> ReindexStaleAsync(int batchSize, CancellationToken cancellationToken)
    {
        var stale = await executor.QueryAsync<Guid>(
            """
            select recipe_id from recipe_search_documents
            where lexicon_version <> @version
            order by recipe_id
            limit @batchSize;
            """,
            new { version = CulinaryLexicon.Version, batchSize },
            cancellationToken).ConfigureAwait(false);

        await WriteConceptsAsync(stale, cancellationToken).ConfigureAwait(false);

        return stale.Count;
    }

    private async Task WriteConceptsAsync(IReadOnlyList<Guid> recipeIds, CancellationToken cancellationToken)
    {
        if (recipeIds.Count == 0)
        {
            return;
        }

        var sources = await executor.QueryAsync<ConceptSource>(
            Sources,
            new { recipeIds = recipeIds.ToArray() },
            cancellationToken).ConfigureAwait(false);

        foreach (var source in sources)
        {
            var concepts = CulinaryLexicon.Describe(source.Title, source.Tags, source.Ingredients);

            await executor.ExecuteAsync(
                """
                update recipe_search_documents
                set concepts = @concepts, lexicon_version = @version
                where recipe_id = @recipeId;
                """,
                new { recipeId = source.RecipeId, concepts = concepts.ToArray(), version = CulinaryLexicon.Version },
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The three things the lexicon reads about a recipe.</summary>
    private const string Sources = """
        select
            r.id as recipe_id,
            r.title,
            array(select t.name from recipe_tags rt
                  join tags t on t.id = rt.tag_id
                  where rt.recipe_id = r.id) as tags,
            array(select i.name from recipe_ingredients i
                  join ingredient_groups g on g.id = i.group_id
                  where g.recipe_id = r.id) as ingredients
        from recipes r
        where r.id = any(@recipeIds);
        """;

    /// <summary>
    /// The same statement the migration runs over every row, narrowed to one.
    /// </summary>
    /// <remarks>
    /// An upsert rather than a delete and an insert, so a concurrent reader
    /// inside another transaction never sees a recipe with no document at all.
    /// </remarks>
    private const string Upsert = """
        insert into recipe_search_documents (
            recipe_id, household_id, language, document, fuzzy_text,
            title_ae, title_a, ingredient_count, analyzer_version)
        select
            recipe_id, household_id, language, document, fuzzy_text,
            title_ae, title_a, ingredient_count, analyzer_version
        from recipe_search_input
        where recipe_id = @recipeId
        on conflict (recipe_id) do update set
            household_id     = excluded.household_id,
            language         = excluded.language,
            document         = excluded.document,
            fuzzy_text       = excluded.fuzzy_text,
            title_ae         = excluded.title_ae,
            title_a          = excluded.title_a,
            ingredient_count = excluded.ingredient_count,
            analyzer_version = excluded.analyzer_version;
        """;

    private sealed record ConceptSource
    {
        public Guid RecipeId { get; init; }

        public string Title { get; init; } = string.Empty;

        public string[] Tags { get; init; } = [];

        public string[] Ingredients { get; init; } = [];
    }
}
