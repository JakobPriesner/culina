using System.Text.Json;
using Domain.Search;

namespace Infrastructure.Persistence.Recipes;

/// <summary>Keeps a recipe's search document in step with the recipe.</summary>
/// <remarks>
/// Called inside the recipe's own write, so the two are never observed apart and an unindexable
/// write fails. The text is built by the database (<c>recipe_search_input</c>) so "what is
/// searchable" is defined once; concepts are the exception because the lexicon is C#.
/// </remarks>
internal sealed class SearchDocumentWriter(DbExecutor executor)
{
    /// <summary>Rebuilds one recipe's document.</summary>
    internal async Task WriteAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        await executor.ExecuteAsync(Upsert, new { recipeId }, cancellationToken).ConfigureAwait(false);
        await WriteConceptsAsync([recipeId], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Rebuilds the concepts of documents a different lexicon built.</summary>
    /// <returns>How many were rebuilt: fewer than asked for means none are left.</returns>
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

        // One statement per batch; JSON because unnest cannot split an array of arrays.
        var documents = sources.Select(source => new
        {
            id = source.RecipeId,
            concepts = CulinaryLexicon.Describe(source.Title, source.Tags, source.Ingredients).ToArray()
        });

        await executor.ExecuteAsync(
            """
            update recipe_search_documents d
            set concepts = batch.concepts, lexicon_version = @version
            from jsonb_to_recordset(@documents::jsonb) as batch(id uuid, concepts text[])
            where d.recipe_id = batch.id;
            """,
            new
            {
                documents = JsonSerializer.Serialize(documents),
                version = CulinaryLexicon.Version
            },
            cancellationToken).ConfigureAwait(false);
    }

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
    /// The migration's statement, narrowed to one row; an upsert so a concurrent reader never sees
    /// no document.
    /// </summary>
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
