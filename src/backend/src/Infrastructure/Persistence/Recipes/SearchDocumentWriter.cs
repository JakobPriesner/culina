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
/// The document is built by the database from the rows that were just written,
/// through the <c>recipe_search_input</c> view. Assembling the text in C#
/// instead would put the definition of "what is searchable about a recipe" in
/// two places — here and in the migration that has to backfill two thousand of
/// them — and the two would drift the first time somebody added a field to one
/// of them.
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
    internal Task WriteAsync(Guid recipeId, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(Upsert, new { recipeId }, cancellationToken);

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
}
